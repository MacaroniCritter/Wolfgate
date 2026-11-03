using Content.Shared._WF.EmoteVoices;
using Content.Shared._WF.Genitals.Profile;
using Content.Shared._WF.Voice;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.Cloning;

/// <summary>Per-character data a genetic record keeps, since the body may be gone by the time it is cloned.</summary>
public sealed class WFCloneTemplate
{
    public SpeechVoice? Voice;

    public List<ProtoId<EmoteVoicePrototype>>? EmoteVoices;

    public bool HasSpeech;

    public ProtoId<SpeechSoundsPrototype>? SpeechSounds;

    public bool HasGenitals;

    public GenitalProfile? Genitals;
}
