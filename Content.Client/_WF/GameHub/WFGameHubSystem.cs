using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Content.Client._WF.DoomArcade;
using Content.Client._WF.DoomArcade.Engine;
using Content.Client._WF.DoomArcade.MeltySynth;
using Content.Shared._WF.GameHub;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._WF.GameHub;

/// <summary>Talks to the server for a game's data and leaderboard, opens games, and plays other players' games nearby.</summary>
public sealed partial class WFGameHubSystem : EntitySystem
{
    public const string DoomGame = "WFGameFreedoom";
    public const string Doom2Game = "WFGameFreedoom2";

    private const float NoiseVolume = -4f;
    private const float MusicVolume = -13.75f;
    private const int MaxNearbyTracks = 2;

    private static readonly ResPath SoundFontPath = new("/Textures/_WF/DoomArcade/TimGM6mb.sf2");

    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WFGameDataManager _data = default!;

    private readonly Dictionary<string, Wad> _packs = new();
    private readonly Dictionary<(string Game, string Sound), AudioStream?> _clips = new();
    private readonly Dictionary<EntityUid, NearbyTrack> _nearby = new();
    private Task<SoundFont>? _soundFont;
    private DoomArcadeWindow? _pdaWindow;
    private TimeSpan _nextCheck;

    /// <summary>Other players' tracks this client is rendering or playing.</summary>
    public int NearbyTrackCount => _nearby.Count;

    /// <summary>A leaderboard arrived from the server.</summary>
    public event Action<WFGameLeaderboardEvent>? LeaderboardReceived;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<WFGameManifestEvent>(ev => _data.SetManifest(ev.Game, ev.Size, ev.Version));
        SubscribeNetworkEvent<WFGameDownloadFailedEvent>(ev => _data.SetFailed(ev.Game));
        SubscribeNetworkEvent<WFGameNoiseHeardEvent>(OnNoiseHeard);
        SubscribeNetworkEvent<WFGameLeaderboardEvent>(ev => LeaderboardReceived?.Invoke(ev));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var track in _nearby.Values)
        {
            StopTrack(track);
        }

        _nearby.Clear();

        foreach (var clip in _clips.Values)
        {
            clip?.Dispose();
        }

        _clips.Clear();

        foreach (var pack in _packs.Values)
        {
            pack.Dispose();
        }

        _packs.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var track in _nearby.Values)
        {
            FinishRender(track);
        }

        if (_timing.RealTime < _nextCheck)
            return;

        _nextCheck = _timing.RealTime + TimeSpan.FromSeconds(1);
        UpdateNearbyTracks();
    }

    public void RequestManifest(string game) => RaiseNetworkEvent(new WFGameManifestRequestEvent(game));

    public void StartDownload(string game)
    {
        _data.BeginDownload(game);
        RaiseNetworkEvent(new WFGameDownloadRequestEvent(game));
    }

    public void RequestLeaderboard(string game) => RaiseNetworkEvent(new WFGameLeaderboardRequestEvent(game));

    public void OpenLeaderboard(string game) => new WFGameLeaderboardWindow(game).OpenCentered();

    /// <summary>Opens a game from a PDA. A game added to the hub needs its window opened here.</summary>
    public void Launch(string game, EntityUid source)
    {
        if (game is not (DoomGame or Doom2Game))
            return;

        _pdaWindow?.Close();
        _pdaWindow = new DoomArcadeWindow();
        _pdaWindow.OpenCentered();
        _pdaWindow.Begin(source, game);
    }

    public void SendNoise(EntityUid source, string game, string sound, float volume)
    {
        var scaled = (byte) Math.Clamp(volume * 255f, 0f, 255f);
        RaiseNetworkEvent(new WFGameNoiseEvent(GetNetEntity(source), game, sound, scaled));
    }

    public void SendMusic(EntityUid source, string game, string? track, bool loop)
    {
        RaiseNetworkEvent(new WFGameBroadcastEvent(GetNetEntity(source), game, track, loop, false));
    }

    public void SendStopped(EntityUid source, string game)
    {
        if (Exists(source))
            RaiseNetworkEvent(new WFGameBroadcastEvent(GetNetEntity(source), game, null, false, true));
    }

    public void SendLevelComplete(EntityUid source, string game, DoomLevelResult result)
    {
        RaiseNetworkEvent(new WFGameLevelCompleteEvent(
            GetNetEntity(source),
            game,
            result.Level,
            result.KillPercent,
            result.ItemPercent,
            result.SecretPercent,
            result.Seconds));
    }

    /// <summary>Plays one sound of another player's game at its source.</summary>
    public bool PlayNearbyNoise(EntityUid source, string game, string sound, byte volume)
    {
        if (TerminatingOrDeleted(source) || GetClip(game, sound) is not { } clip)
            return false;

        var parameters = AudioParams.Default
            .WithVolume(NoiseVolume + SharedAudioSystem.GainToVolume(MathF.Max(volume / 255f, 0.01f)))
            .WithMaxDistance(WFGameHubProtocol.HearRange);

        return _audioSystem.PlayEntity(clip, source, null, parameters) != null;
    }

    private void OnNoiseHeard(WFGameNoiseHeardEvent ev)
    {
        if (TryGetEntity(ev.Source, out var source))
            PlayNearbyNoise(source.Value, ev.Game, ev.Sound, ev.Volume);
    }

    private void UpdateNearbyTracks()
    {
        var listener = _player.LocalEntity;
        var seen = new HashSet<EntityUid>();

        if (listener != null)
        {
            var listenerPosition = _transform.GetMapCoordinates(listener.Value);
            var query = EntityQueryEnumerator<WFGameBroadcastComponent>();
            while (query.MoveNext(out var uid, out var broadcast))
            {
                // The player's own client plays this music directly.
                if (broadcast.Player == _player.LocalUser ||
                    broadcast.Track == null ||
                    !_transform.GetMapCoordinates(uid).InRange(listenerPosition, WFGameHubProtocol.HearRange))
                {
                    continue;
                }

                seen.Add(uid);
                if (_nearby.TryGetValue(uid, out var track))
                {
                    if (track.Name == broadcast.Track)
                        continue;

                    StopTrack(track);
                    _nearby.Remove(uid);
                }

                if (_nearby.Count < MaxNearbyTracks)
                    StartTrack(uid, broadcast);
            }
        }

        var stale = new List<EntityUid>();
        foreach (var (uid, track) in _nearby)
        {
            if (seen.Contains(uid))
                continue;

            StopTrack(track);
            stale.Add(uid);
        }

        foreach (var uid in stale)
        {
            _nearby.Remove(uid);
        }
    }

    private void StartTrack(EntityUid uid, WFGameBroadcastComponent broadcast)
    {
        var pack = GetPack(broadcast.Game);
        if (pack == null || pack.GetLumpNumber(broadcast.Track!) == -1)
            return;

        var midi = pack.ReadLump(broadcast.Track!);
        var soundFont = _soundFont ??= LoadSoundFont();
        var cancel = new CancellationTokenSource();
        var token = cancel.Token;
        var sawmill = Log;

        _nearby[uid] = new NearbyTrack
        {
            Source = uid,
            Name = broadcast.Track!,
            Loop = broadcast.Loop,
            Cancel = cancel,
            Render = Task.Run(async () =>
            {
                try
                {
                    return DoomArcadeMusic.Render(await soundFont, midi, token);
                }
                catch (Exception e)
                {
                    sawmill.Error($"Nearby game music failed to render: {e}");
                    return null;
                }
            }),
        };
    }

    private void FinishRender(NearbyTrack track)
    {
        if (track.Render is not { IsCompleted: true } render)
            return;

        track.Render = null;

#pragma warning disable RA0004 // IsCompleted was checked above, so this can't block.
        if (render.Result is not { Length: > 0 } samples)
            return;
#pragma warning restore RA0004

        if (TerminatingOrDeleted(track.Source))
            return;

        // A sound with a position in the world has to be mono.
        var mono = new short[samples.Length / 2];
        for (var i = 0; i < mono.Length; i++)
        {
            mono[i] = (short) ((samples[i * 2] + samples[i * 2 + 1]) / 2);
        }

        track.Clip = _audioManager.LoadAudioRaw(mono, 1, DoomArcadeMusic.SampleRate);
        var parameters = AudioParams.Default
            .WithVolume(MusicVolume)
            .WithMaxDistance(WFGameHubProtocol.HearRange)
            .WithLoop(track.Loop);
        track.Playing = _audioSystem.PlayEntity(track.Clip, track.Source, null, parameters)?.Entity;
    }

    private void StopTrack(NearbyTrack track)
    {
        track.Cancel.Cancel();
        _audioSystem.Stop(track.Playing);
        track.Playing = null;
        track.Clip?.Dispose();
        track.Clip = null;
    }

    private Task<SoundFont> LoadSoundFont()
    {
        var bytes = ReadResource(SoundFontPath);
        return Task.Run(() => new SoundFont(bytes));
    }

    private AudioStream? GetClip(string game, string sound)
    {
        if (_clips.TryGetValue((game, sound), out var clip))
            return clip;

        var pack = GetPack(game);
        if (pack != null && pack.GetLumpNumber(sound) != -1 &&
            DoomArcadeSound.DecodeClip(pack.ReadLump(sound), out var sampleRate, out _) is { } samples)
        {
            clip = _audioManager.LoadAudioRaw(samples, 1, sampleRate);
        }

        _clips[(game, sound)] = clip;
        return clip;
    }

    private Wad? GetPack(string game)
    {
        if (_packs.TryGetValue(game, out var pack))
            return pack;

        if (!_protos.TryIndex<WFGamePrototype>(game, out var proto))
            return null;

        pack = new Wad(proto.DataFile, ReadResource(proto.NoisePack));
        _packs[game] = pack;
        return pack;
    }

    private MemoryStream ReadResource(ResPath path)
    {
        var memory = new MemoryStream();
        using (var file = _resources.ContentFileRead(path))
        {
            file.CopyTo(memory);
        }

        memory.Position = 0;
        return memory;
    }

    private sealed class NearbyTrack
    {
        public EntityUid Source;
        public string Name = string.Empty;
        public bool Loop;
        public CancellationTokenSource Cancel = default!;
        public Task<short[]?>? Render;
        public AudioStream? Clip;
        public EntityUid? Playing;
    }
}
