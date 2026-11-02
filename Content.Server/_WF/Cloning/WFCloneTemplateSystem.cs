using Content.Server._WF.EmoteVoices;
using Content.Server._WF.Genitals;
using Content.Server._WF.Voice;
using Content.Shared._WF.Genitals.Components;
using Content.Shared._WF.Voice;
using Content.Shared.Speech;

namespace Content.Server._WF.Cloning;

/// <summary>Stores Wolfgate's per-character data at scan time and restores it on the clone.</summary>
public sealed class WFCloneTemplateSystem : EntitySystem
{
    [Dependency] private EmoteVoiceSystem _emoteVoice = default!;
    [Dependency] private GenitalOrganSystem _genitals = default!;

    /// <summary>Reads the template off a body being scanned.</summary>
    public WFCloneTemplate Capture(EntityUid body)
    {
        var template = new WFCloneTemplate
        {
            EmoteVoices = _emoteVoice.GetVoices(body),
        };

        if (TryComp<SpeechVoiceComponent>(body, out var voice))
            template.Voice = voice.Voice.Clone();

        if (TryComp<SpeechComponent>(body, out var speech))
        {
            template.HasSpeech = true;
            template.SpeechSounds = speech.SpeechSounds;
        }

        if (TryComp<GenitalsComponent>(body, out var genitals))
        {
            template.HasGenitals = true;
            template.Genitals = genitals.SourceProfile;
        }

        return template;
    }

    /// <summary>Applies a template to a clone whose appearance is already set.</summary>
    public void Apply(EntityUid clone, WFCloneTemplate template)
    {
        if (template.EmoteVoices != null)
            _emoteVoice.SetVoices(clone, template.EmoteVoices);

        if (template.HasSpeech && TryComp<SpeechComponent>(clone, out var speech))
            speech.SpeechSounds = template.SpeechSounds;

        if (template.Voice != null)
        {
            var voice = EnsureComp<SpeechVoiceComponent>(clone);
            voice.Voice = template.Voice.Clone();
            Dirty(clone, voice);
        }

        if (template.HasGenitals)
            _genitals.ApplyCloneTemplate(clone, template.Genitals);
    }
}
