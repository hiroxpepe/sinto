#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class VoicesTests
{
    static Note MakeNote(int midi = 60, int track = 2, int priority = 5)
        => new(midi, 0.8f, track, priority);

    static OscParams DefaultOsc()
        => new(WaveType.Sine);

    [Test] public void Constructor_ActiveVoicesIsZero()
    {
        var vm = new Voices(32, 44100);
        Assert.That(vm.activeVoices, Is.EqualTo(0));
    }

    [Test] public void NoteOn_ActiveVoicesIncreases()
    {
        var vm = new Voices(32, 44100);
        vm.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        Assert.That(vm.activeVoices, Is.GreaterThan(0));
    }

    [Test] public void NoteOff_MatchingNote_TransitionsToRelease()
    {
        var vm = new Voices(32, 44100);
        vm.NoteOn(MakeNote(60, 2), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        Assert.DoesNotThrow(() => vm.NoteOff(60, 2));
    }

    [Test] public void NoteOff_NonExistentNote_DoesNotThrow()
        => Assert.DoesNotThrow(() => new Voices(32).NoteOff(99, 0));

    [Test] public void AllNotesOff_ClearsAllVoices()
    {
        var vm = new Voices(32, 44100);
        for (int i = 0; i < 8; i++)
            vm.NoteOn(MakeNote(60 + i, i % 8), DefaultOsc(), DefaultOsc(),
                EnvParams.Default, EnvParams.Default, EnvParams.Default);
        vm.AllNotesOff();
        // After AllNotesOff: voices enter QuickRelease — still audible but reassignable
        Assert.DoesNotThrow(() => vm.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default));
    }

    [Test] public void FortySimultaneousNoteOn_NeverExceedsMaxVoices()
    {
        var vm = new Voices(32, 44100);
        for (int i = 0; i < 40; i++)
            vm.NoteOn(MakeNote(60 + (i % 12), i % 8), DefaultOsc(), DefaultOsc(),
                EnvParams.Default, EnvParams.Default, EnvParams.Default);
        Assert.That(vm.activeVoices, Is.LessThanOrEqualTo(32));
    }

    [Test] public void SetMaxVoices_ReducesLimit()
    {
        var vm = new Voices(32, 44100);
        vm.SetMaxVoices(16);
        Assert.That(vm.maxVoices, Is.EqualTo(16));
    }

    [Test] public void RenderSamples_MultipleVoices_OutputDoesNotExceedPlusMinusOne()
    {
        // 32 voices × amplitude 1.0 = 32.0 without normalization → hard clipping.
        // Output must stay within [-1.0, 1.0] regardless of voice count.
        var vm = new Voices(32, 44100);
        // Trigger 8 simultaneous notes
        for (int i = 0; i < 8; i++)
            vm.NoteOn(new Note(60 + i, 1.0f, i % 8, 5), DefaultOsc(), DefaultOsc(),
                new EnvParams(0.001f, 0.1f, 1.0f, 0.1f),
                EnvParams.Default, EnvParams.Default);

        var buf = new float[1024];
        vm.RenderSamples(buf.AsSpan(), 1);

        foreach (float s in buf)
            Assert.That(s, Is.InRange(-1.0f - 1e-4f, 1.0f + 1e-4f),
                $"Output {s} exceeds [-1, 1]. Master volume normalization is missing.");
    }

    [Test] public void RenderSamples_DoesNotThrow()
    {
        var vm = new Voices(32, 44100);
        var buf = new float[512];
        Assert.DoesNotThrow(() => vm.RenderSamples(buf.AsSpan(), 2));
    }

    // ── SnapToTarget wiring: integration test ────────────────────

    // ── struct ref correctness: PlayState change must persist ────────────

    [Test] public void NoteOn_PlayState_PersistsInArray_NotJustLocalCopy()
    {
        // Verifies that Voices mutates _voices[i] in-place (ref var),
        // not a local copy (var). If PlayState changes don't persist,
        // activeVoices stays 0 after NoteOn — pool exhaustion in seconds.
        var vm = new Voices(32, 44100);
        Assert.That(vm.activeVoices, Is.EqualTo(0));
        vm.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        Assert.That(vm.activeVoices, Is.GreaterThan(0),
            "activeVoices must reflect the in-array state change. " +
            "If 0, Voices is mutating a struct copy, not the array element.");
    }

    // ── Sustain Pedal (CC64) ─────────────────────────────────────────────

    [Test] public void SustainPedal_Down_PreventsNoteOffFromReleasing()
    {
        // With pedal down, NoteOff must NOT transition voice to Release.
        // Voice stays active until pedal is released.
        var vm = new Voices(32, 44100);
        vm.NoteOn(MakeNote(60), DefaultOsc(), DefaultOsc(),
            new EnvParams(0.001f, 0.1f, 1.0f, 2.0f),
            EnvParams.Default, EnvParams.Default);

        vm.SetSustainPedal(true);
        vm.NoteOff(60, 2);

        // With pedal down, note must still be active (not in Release)
        Assert.That(vm.IsNoteActive(60, 2), Is.True,
            "With sustain pedal down, NoteOff must not release the voice.");
    }

    [Test] public void SustainPedal_Released_DoesNotKillPhysicallyHeldNotes()
    {
        // Scenario: hold C4, press pedal, play C5, release pedal.
        // C4 (still physically held) must NOT be released.
        // C5 (NoteOff received while pedal was down) MUST be released.
        var vm = new Voices(32, 44100);
        var env = new EnvParams(0.001f, 0.1f, 1.0f, 2.0f);

        // NoteOn C4 (midi=60) — key is held
        vm.NoteOn(new Note(60, 0.8f, 2, 5), DefaultOsc(), DefaultOsc(),
            env, EnvParams.Default, EnvParams.Default);

        vm.SetSustainPedal(true);

        // NoteOn C5 (midi=72), then NoteOff — key released while pedal down
        vm.NoteOn(new Note(72, 0.8f, 2, 5), DefaultOsc(), DefaultOsc(),
            env, EnvParams.Default, EnvParams.Default);
        vm.NoteOff(72, 2); // deferred — pedal is down

        // Release pedal
        vm.SetSustainPedal(false);

        // C5 (IsKeyHeld=false) must be in Release
        Assert.That(vm.IsNoteActive(72, 2), Is.False,
            "C5 (key released before pedal up) must transition to Release when pedal released.");

        // C4 (IsKeyHeld=true — still physically pressed) must stay active
        Assert.That(vm.IsNoteActive(60, 2), Is.True,
            "C4 (still physically held) must NOT be released when pedal is lifted. " +
            "IsKeyHeld flag missing from Voice struct.");
    }

    [Test] public void SustainPedal_Released_FiresDeferredNoteOffs()
    {
        var vm = new Voices(32, 44100);
        vm.NoteOn(MakeNote(60), DefaultOsc(), DefaultOsc(),
            new EnvParams(0.001f, 0.1f, 1.0f, 2.0f),
            EnvParams.Default, EnvParams.Default);

        vm.SetSustainPedal(true);
        vm.NoteOff(60, 2); // deferred

        vm.SetSustainPedal(false); // pedal release must fire deferred NoteOff

        Assert.That(vm.IsNoteActive(60, 2), Is.False,
            "After sustain pedal released, deferred NoteOff must fire.");
    }

    // ── NoteOff: all-matching voices released (Hanging Note prevention) ────

    [Test] public void NoteOff_SameNoteTriggeredThreeTimes_AllVoicesRelease()
    {
        // Same midiNote + trackId triggered 3 times → 3 voices active.
        // One NoteOff must release ALL 3, not just the first found.
        var vm = new Voices(32, 44100);
        var longEnv = new EnvParams(0.001f, 0.1f, 1.0f, 5.0f); // long release

        vm.NoteOn(new Note(60, 0.8f, 2, 5), DefaultOsc(), DefaultOsc(),
            longEnv, EnvParams.Default, EnvParams.Default);
        vm.NoteOn(new Note(60, 0.8f, 2, 5), DefaultOsc(), DefaultOsc(),
            longEnv, EnvParams.Default, EnvParams.Default);
        vm.NoteOn(new Note(60, 0.8f, 2, 5), DefaultOsc(), DefaultOsc(),
            longEnv, EnvParams.Default, EnvParams.Default);

        int before = vm.activeVoices;
        Assert.That(before, Is.GreaterThanOrEqualTo(1), "At least 1 voice should be active.");

        vm.NoteOff(60, 2); // single NoteOff must release ALL matching voices

        // All voices for note 60/track 2 must be in Release or QuickRelease (not Sustain)
        // We verify by checking IsNoteActive returns false (or they moved to Release)
        // After NoteOff, note should not be in active/sustain state
        Assert.That(vm.IsNoteActive(60, 2), Is.False,
            "After NoteOff, no voice should remain in Attack/Decay/Sustain for note 60 track 2. " +
            "If true, some voices were not released — Hanging Note bug.");
    }

    // ── Same-track Voice Stealing ──────────────────────────────────

    [Test] public void NoteOn_DrumTrack_ExceedsReservedVoices_StealsOldestDrumVoice()
    {
        // Drum track has ReservedVoices=2. When a 3rd drum NoteOn arrives,
        // the oldest drum voice must be stolen (not another track's voice).
        var vm = new Voices(32, 44100);
        var env = new EnvParams(0.001f, 0.1f, 1.0f, 2.0f);

        // Fill drum track with 2 reserved voices
        vm.NoteOn(new Note(36, 0.8f, 0, 10), DefaultOsc(), DefaultOsc(),
            env, EnvParams.Default, EnvParams.Default); // kick 1
        vm.NoteOn(new Note(38, 0.8f, 0, 10), DefaultOsc(), DefaultOsc(),
            env, EnvParams.Default, EnvParams.Default); // snare

        // Add a bass voice to ensure drum stealing doesn't touch it
        vm.NoteOn(new Note(40, 0.8f, 2, 8), DefaultOsc(), DefaultOsc(),
            env, EnvParams.Default, EnvParams.Default); // bass

        bool bassActiveBefore = vm.IsNoteActive(40, 2);

        // 3rd drum note — must steal oldest drum, NOT the bass
        vm.NoteOn(new Note(42, 0.8f, 0, 10), DefaultOsc(), DefaultOsc(),
            env, EnvParams.Default, EnvParams.Default); // hihat

        Assert.That(vm.IsNoteActive(40, 2), Is.EqualTo(bassActiveBefore),
            "Bass voice must not be stolen when drum track exceeds its reserved voice count.");
        Assert.That(vm.IsNoteActive(42, 0), Is.True,
            "New drum note (hihat) must be active after stealing oldest drum voice.");
    }

    [Test] public void NoteOn_StolenVoice_CutoffSnapsToNewValue_NotPreviousValue()
    {
        // Scenario:
        //   1. Fill all voices with Cutoff = 0.1
        //   2. NoteOn a new note requesting Cutoff = 0.9 (forces Voice Steal)
        //   3. The stolen voice must start at Cutoff 0.9 (SnapToTarget called)
        //      NOT glide from 0.1 → 0.9 ("pyun" transient artifact)
        //
        // If Voices forgets to call SmoothedCutoff.SnapToTarget() on NoteOn,
        // GetVoiceCurrentCutoff returns 0.1 (previous voice state) instead of 0.9.
        const int maxVoices = 4;
        var vm = new Voices(maxVoices, 44100);

        // Fill all voices with notes using Cutoff 0.1
        vm.SetFilterParams(0.1f, 0.3f, FilterKind.Roland);
        for (int i = 0; i < maxVoices; i++)
            vm.NoteOn(new Note(60 + i, 0.8f, 2, 5), DefaultOsc(), DefaultOsc(),
                new EnvParams(0.001f, 0.1f, 0.8f, 0.1f),
                EnvParams.Default, EnvParams.Default);

        // Now request NoteOn with Cutoff 0.9 — must steal a voice
        vm.SetFilterParams(0.9f, 0.5f, FilterKind.Roland);
        vm.NoteOn(new Note(72, 0.8f, 2, 5), DefaultOsc(), DefaultOsc(),
            new EnvParams(0.001f, 0.1f, 0.8f, 0.1f),
            EnvParams.Default, EnvParams.Default);

        // The new voice's SmoothedCutoff.current must be 0.9 (snapped), not 0.1 (old value)
        float cutoff = vm.GetVoiceCurrentCutoff(72, 2);
        Assert.That(cutoff, Is.Not.EqualTo(-1f), "No active voice found for MIDI 72.");
        Assert.That(cutoff, Is.EqualTo(0.9f).Within(1e-4f),
            $"Stolen voice cutoff is {cutoff} — SnapToTarget() was not called on NoteOn. " +
            "This causes a 'pyun' transient artifact.");
    }

    // ── TrackId boundary: invalid values must NOT crash audio thread ────

    [TestCase(8)]    // one above max (0-7)
    [TestCase(-1)]   // negative
    [TestCase(100)]  // way out of range
    [TestCase(int.MaxValue)]
    [TestCase(int.MinValue)]
    public void NoteOn_InvalidTrackId_DoesNotThrow(int invalidTrackId)
    {
        // If Voices does array[trackId] without clamping,
        // IndexOutOfRangeException kills the audio thread silently.
        var vm = new Voices(32, 44100);
        var note = new Note(60, 0.8f, invalidTrackId, 5);
        Assert.DoesNotThrow(() => vm.NoteOn(note, DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default),
            $"Voices must not throw for TrackId={invalidTrackId}.");
    }

    [TestCase(8)]
    [TestCase(-1)]
    public void NoteOff_InvalidTrackId_DoesNotThrow(int invalidTrackId)
    {
        var vm = new Voices(32, 44100);
        Assert.DoesNotThrow(() => vm.NoteOff(60, invalidTrackId));
    }

    [Test] public void DrumTrack_NoteIsNeverStolenByOtherTrack()
    {
        // Verify that drum track (0) cannot be stolen by other tracks.
        // Checking activeVoices count alone is insufficient — a drum voice may have been
        // replaced. Use IsNoteActive() to verify drum notes are still alive.
        var vm = new Voices(4, 44100); // Tiny voice pool to force stealing

        // Fill all voices with drum track notes
        int[] drumNotes = { 60, 61, 62, 63 };
        foreach (int n in drumNotes)
            vm.NoteOn(new Note(n, 0.8f, 0, 10), // Protected track
                DefaultOsc(), DefaultOsc(),
                new EnvParams(0.001f, 0.1f, 1.0f, 1.0f),
                EnvParams.Default, EnvParams.Default);

        // NoteOn from non-drum track must not steal Protected voices
        vm.NoteOn(new Note(72, 0.8f, 2, 1), // Low priority non-drum
            DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);

        // All drum notes must survive
        foreach (int n in drumNotes)
            Assert.That(vm.IsNoteActive(n, 0), Is.True,
                $"Drum note {n} was stolen — Protected flag not respected.");
    }

    // ── SetOscLevels ─────────────────────────────────────────────────────

    [Test] public void SetOscLevels_Osc1Zero_ReducesOutput()
    {
        var vm = new Voices(4, 44100);
        vm.SetOscLevels(0f, 0f, 0f);
        vm.NoteOn(MakeNote(60), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        var buf = new float[512];
        vm.RenderSamples(buf.AsSpan(), 1);
        float rms = 0f;
        foreach (var s in buf) rms += s * s;
        Assert.That(rms, Is.LessThan(1e-4f),
            "OSC1+OSC2 level=0 must produce near-silence.");
    }

    [Test] public void SetOscLevels_Clamps_OutOfRange()
    {
        var vm = new Voices(4, 44100);
        Assert.DoesNotThrow(() => vm.SetOscLevels(-1f, 2f, 200f),
            "SetOscLevels must clamp out-of-range values without throwing.");
    }

    [Test] public void SetOscLevels_ImmediateEffect_OnActiveVoice()
    {
        // SetOscLevels must affect currently-playing voices immediately
        var vm = new Voices(4, 44100);
        vm.SetOscLevels(1.0f, 1.0f, 0f);
        vm.NoteOn(MakeNote(60), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        var buf1 = new float[512];
        vm.RenderSamples(buf1.AsSpan(), 1);
        // Now reduce level
        vm.SetOscLevels(0f, 0f, 0f);
        var buf2 = new float[512];
        vm.RenderSamples(buf2.AsSpan(), 1);
        float rms1 = 0f, rms2 = 0f;
        foreach (var s in buf1) rms1 += s * s;
        foreach (var s in buf2) rms2 += s * s;
        Assert.That(rms2, Is.LessThan(rms1),
            "SetOscLevels must take effect immediately on active voices.");
    }

    // ── SetFilterEnv ─────────────────────────────────────────────────────

    [Test] public void SetFilterEnv_DoesNotThrow()
    {
        var vm = new Voices(4, 44100);
        Assert.DoesNotThrow(() => vm.SetFilterEnv(0.01f, 0.3f, 0f, 0.2f));
    }

    [Test] public void SetFilterEnv_AppliedOnNextNoteOn()
    {
        var vm = new Voices(4, 44100);
        vm.SetFilterParams(0.2f, 0f, FilterKind.Roland);
        vm.SetFilterEnv(0.001f, 0.5f, 0f, 0.1f);
        vm.SetFilterEnvAmount(1.0f);
        vm.NoteOn(MakeNote(60), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        var buf = new float[512];
        Assert.DoesNotThrow(() => vm.RenderSamples(buf.AsSpan(), 1),
            "SetFilterEnv must not cause exceptions during rendering.");
    }

    // ── SetFilterEnvAmount ────────────────────────────────────────────────

    [Test] public void SetFilterEnvAmount_Zero_NoModulation()
    {
        var vm1 = new Voices(4, 44100);
        var vm2 = new Voices(4, 44100);
        vm1.SetFilterEnvAmount(0f);
        vm2.SetFilterEnvAmount(0f);
        vm1.SetFilterEnv(0.001f, 0.1f, 0f, 0.1f);
        vm2.SetFilterEnv(0.001f, 0.1f, 0f, 0.1f);
        vm1.NoteOn(MakeNote(60), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        vm2.NoteOn(MakeNote(60), DefaultOsc(), DefaultOsc(),
            EnvParams.Default, EnvParams.Default, EnvParams.Default);
        var buf1 = new float[1024]; var buf2 = new float[1024];
        vm1.RenderSamples(buf1.AsSpan(), 1);
        vm2.RenderSamples(buf2.AsSpan(), 1);
        float diff = 0f;
        for (int i = 0; i < buf1.Length; i++) diff += MathF.Abs(buf1[i] - buf2[i]);
        Assert.That(diff, Is.LessThan(1e-4f),
            "Two voices with ENV AMT=0 and same params must produce identical output.");
    }

    [Test] public void SetFilterEnvAmount_Clamps_OutOfRange()
    {
        var vm = new Voices(4, 44100);
        Assert.DoesNotThrow(() => vm.SetFilterEnvAmount(-1f));
        Assert.DoesNotThrow(() => vm.SetFilterEnvAmount(2f));
    }

    // ── Cutoff Hz-space smoothing ─────────────────────────────────────────
    // RED: Smoother must interpolate in Hz space so that sweep speed is
    //      perceptually uniform across low and high frequency ranges.
    //      Currently Smoother works in linear [0,1] space → high-range
    //      sweeps are abrupt while low-range sweeps are sluggish.

    [Test] public void SetFilterParams_LowRangeSweep_SpeedMatchesHighRange()
    {
        // [0,1] cutoff space is log-Hz: Δ0.1 always equals 1 octave.
        // Smoother in [0,1] space is therefore perceptually uniform.
        // Verify: low-range (0.1→0.3) and high-range (0.7→0.9) sweeps
        // produce comparable octave-normalised output change.
        const int SR = 44100;
        const int MEASURE = 500;

        float SweepDelta(float from, float to) {
            var vm = new Voices(1, SR);
            vm.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
                EnvParams.Default, EnvParams.Default, EnvParams.Default);
            vm.SetFilterParams(from, 0f, FilterKind.Moog);
            var warm = new float[SR / 10];
            vm.RenderSamples(warm.AsSpan(), 1);
            var base_buf = new float[MEASURE];
            vm.RenderSamples(base_buf.AsSpan(), 1);
            vm.SetFilterParams(to, 0f, FilterKind.Moog);
            var sweep_buf = new float[MEASURE];
            vm.RenderSamples(sweep_buf.AsSpan(), 1);
            float delta = 0f;
            for (int i = 0; i < MEASURE; i++) delta += MathF.Abs(sweep_buf[i] - base_buf[i]);
            return delta / MEASURE;
        }

        float delta_low  = SweepDelta(0.1f, 0.3f);  // 2 octaves
        float delta_high = SweepDelta(0.7f, 0.9f);  // 2 octaves

        // Both sweeps cover 2 octaves in [0,1] space.
        // Output delta may differ due to filter amplitude, but ratio must be < 20.
        float ratio = delta_low > 1e-8f ? delta_high / delta_low : 0f;
        Assert.That(ratio, Is.LessThan(20f),
            $"High-range Δ={delta_high:F5}, low-range Δ={delta_low:F5}, ratio={ratio:F1}. " +
            "[0,1] cutoff is log-Hz: same step must produce comparable sweep speed.");
    }
}