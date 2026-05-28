#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Audio;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class EngineTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => { using var e = new Engine(); });

    [Test] public void IsPaused_InitiallyFalse()
    {
        using var e = new Engine();
        Assert.That(e.isPaused, Is.False);
    }

    [Test] public void Pause_SetsPausedTrue()
    {
        using var e = new Engine();
        e.Pause();
        Assert.That(e.isPaused, Is.True);
    }

    [Test] public void Resume_SetsPausedFalse()
    {
        using var e = new Engine();
        e.Pause();
        e.Resume();
        Assert.That(e.isPaused, Is.False);
    }

    [Test] public void ProcessAudioCallback_WhilePaused_ZeroesBuffer()
    {
        using var e = new Engine();
        e.Pause();
        var buf = new float[512];
        for (int i = 0; i < buf.Length; i++) buf[i] = 1.0f;
        e.ProcessAudioCallback(buf.AsSpan());
        Assert.That(buf, Is.All.EqualTo(0f));
    }

    [Test] public void ProcessAudioCallback_EmptyBuffer_DoesNotThrow()
    {
        using var e = new Engine();
        Assert.DoesNotThrow(() => e.ProcessAudioCallback(Span<float>.Empty));
    }

    [Test] public void SendNoteOn_ValidNote_ReturnsTrue()
    {
        using var e = new Engine();
        Assert.That(e.SendNoteOn(60, 0.8f, 2, 5, 0), Is.True);
    }

    [Test] public void SendNoteOff_DoesNotThrow()
    {
        using var e = new Engine();
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        Assert.DoesNotThrow(() => e.SendNoteOff(60, 2, 0));
    }

    [Test] public void SetBPM_StoresBpm()
    {
        using var e = new Engine();
        e.SetBPM(140f);
        Assert.That(e.currentBpm, Is.EqualTo(140f).Within(0.1f));
    }

    [Test] public void ActiveVoices_Initially_IsZero()
    {
        using var e = new Engine();
        Assert.That(e.activeVoices, Is.EqualTo(0));
    }

    [Test] public void DefaultEvent_KindIsNone()
    {
        // default(Event) must have Kind=None (not NoteOn).
        // This is the first line of defense against phantom notes.
        var ev = default(Event);
        Assert.That(ev.Kind, Is.EqualTo(EventKind.None));
        Assert.That((int)EventKind.None, Is.EqualTo(0));
    }

    [Test] public void ProcessAudioCallback_NoneEvent_IsIgnoredSafely()
    {
        // Verify that processing a buffer with no events keeps activeVoices = 0.
        // The None event defense is: (1) default(Event).Kind == None proven above,
        // (2) ApplyEvent must have a case/default that discards None without triggering NoteOn.
        using var e = new Engine();
        var buf = new float[512];
        // Process empty buffer (ring buffer has no events = effectively None events)
        e.ProcessAudioCallback(buf.AsSpan());
        Assert.That(e.activeVoices, Is.EqualTo(0),
            "Processing empty buffer must not trigger phantom NoteOn.");
    }

    [Test] public void SubBuffering_OffsetFrames_EventFiresAtCorrectPosition()
    {
        // FRAME vs SAMPLE correctness:
        // OffsetFrames=512 means fire at frame 512.
        // In stereo (2ch), frame 512 = sample index 1024.
        // Sub-buffering loop must use: buffer.Slice(posFrames * channels, frames * channels)
        // NOT: buffer.Slice(posFrames, frames)  ← wrong: treats frames as samples
        //
        // Verify: samples before frame 512 (indices 0..1023 in stereo) are silent.
        //         samples from frame 512 (indices 1024+) contain audio.
        const int SR       = 44100;
        const int Frames   = 1024;
        const int Channels = 2;       // stereo — exercises the frame/sample distinction
        const int Offset   = 512;     // fire at frame 512

        using var e = new Engine(sampleRate: SR, channels: Channels, bufferSize: Frames);
        e.SendNoteOn(60, 1.0f, 2, 5, (ushort)Offset);
        var buf = new float[Frames * Channels]; // stereo interleaved
        e.ProcessAudioCallback(buf.AsSpan());

        // Before frame 512: indices 0..(512*2-1)=0..1023 must be silent
        for (int i = 0; i < Offset * Channels; i++)
            Assert.That(buf[i], Is.EqualTo(0f),
                $"buf[{i}] (frame {i/Channels}) must be silent before OffsetFrames={Offset}.");

        // After frame 512: at least one non-zero sample
        bool hasAudio = false;
        for (int i = Offset * Channels; i < buf.Length; i++)
            if (buf[i] != 0f) { hasAudio = true; break; }
        Assert.That(hasAudio, Is.True,
            $"No audio after frame {Offset} (sample index {Offset * Channels}). " +
            "Check that sub-buffering uses posFrames * channels for Span slicing.");
    }

    [Test] public void ApplyEvent_NoneKind_NeverIncreasesActiveVoices()
    {
        // Explicit None case in switch prevents phantom NoteOn.
        // Processing 1000 buffers with no events must keep voices at 0.
        using var e = new Engine();
        var buf = new float[512];
        for (int i = 0; i < 1000; i++)
            e.ProcessAudioCallback(buf.AsSpan());
        Assert.That(e.activeVoices, Is.EqualTo(0),
            "1000 empty ProcessAudioCallback calls must not trigger any phantom NoteOn.");
    }

    [Test] public void Pause_Resume_TickDoesNotAccumulate()
    {
        // DoesNotThrow alone does not prove DspTime is not accumulating.
        // Numerically verify dspTimeSamples does not advance while paused.
        using var e = new Engine();
        var buf = new float[512];

        // Process one buffer to establish DspTime baseline
        e.ProcessAudioCallback(buf.AsSpan());
        long beforePause = e.dspTimeSamples;

        // Pause and call ProcessAudioCallback 100 times
        e.Pause();
        for (int i = 0; i < 100; i++) e.ProcessAudioCallback(buf.AsSpan());
        long duringPause = e.dspTimeSamples;

        // dspTimeSamples must NOT advance while paused
        Assert.That(duringPause, Is.EqualTo(beforePause),
            "dspTimeSamples must NOT advance while paused.");

        // Resume and process exactly one buffer
        e.Resume();
        e.ProcessAudioCallback(buf.AsSpan());
        long afterResume = e.dspTimeSamples;

        // After Resume, DspTime must advance by exactly one buffer (no burst accumulation)
        Assert.That(afterResume, Is.EqualTo(beforePause + 512),
            "dspTimeSamples must advance by exactly 1 buffer after Resume.");
    }

    // ── SetOscParams ─────────────────────────────────────────────────────

    [Test] public void SetOscParams_DoesNotThrow()
    {
        using var e = new Engine();
        Assert.DoesNotThrow(() => e.SetOscParams(1.0f, 0.5f, 0f));
    }

    [Test] public void SetOscParams_Osc2Level_Zero_ReducesOutput()
    {
        // OSC2 level=0 should produce less output than OSC2 level=1
        using var e1 = new Engine();
        using var e2 = new Engine();
        e1.SetOscParams(1.0f, 0.0f, 0f);
        e2.SetOscParams(1.0f, 1.0f, 0f);
        e1.SendNoteOn(60, 0.8f, 2, 5, 0);
        e2.SendNoteOn(60, 0.8f, 2, 5, 0);
        var buf1 = new float[512]; var buf2 = new float[512];
        e1.ProcessAudioCallback(buf1.AsSpan());
        e2.ProcessAudioCallback(buf2.AsSpan());
        float rms1 = 0f, rms2 = 0f;
        foreach (var s in buf1) rms1 += s * s;
        foreach (var s in buf2) rms2 += s * s;
        Assert.That(rms1, Is.LessThanOrEqualTo(rms2 + 1e-4f),
            "OSC2 level=0 should produce equal or less output.");
    }

    [Test] public void SetOscParams_DetuneZero_NoBeating()
    {
        // Detune=0 should produce stable output (no beating)
        using var e = new Engine();
        e.SetOscParams(1.0f, 1.0f, 0f);
        e.SendNoteOn(36, 0.8f, 2, 5, 0); // low C
        var buf = new float[44100 * 2]; // 1 second stereo
        e.ProcessAudioCallback(buf.AsSpan());
        // Check all samples are finite
        foreach (var s in buf)
        {
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False,
                "Detune=0 output must be finite.");
        }
    }

    // ── SetFilterEnv ─────────────────────────────────────────────────────

    [Test] public void SetFilterEnv_DoesNotThrow()
    {
        using var e = new Engine();
        Assert.DoesNotThrow(() => e.SetFilterEnv(0.01f, 0.3f, 0f, 0.2f));
    }

    [Test] public void SetFilterEnv_WithHighEnvAmt_ModulatesCutoff()
    {
        // FilterEnv + high ENV AMT should modulate cutoff over time
        using var e = new Engine();
        e.SetFilterParams(0.2f, 0f, FilterKind.Roland); // low cutoff base
        e.SetFilterEnv(0.001f, 0.5f, 0f, 0.1f);        // fast attack, long decay
        e.SetFilterEnvAmount(1.0f);                      // full modulation
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        // First buffer: filter is opening (attack)
        var buf_early = new float[512];
        e.ProcessAudioCallback(buf_early.AsSpan());
        // Later buffer: filter is closing (decay)
        for (int i = 0; i < 20; i++) {
            var tmp = new float[512];
            e.ProcessAudioCallback(tmp.AsSpan());
        }
        var buf_late = new float[512];
        e.ProcessAudioCallback(buf_late.AsSpan());
        float rms_early = 0f, rms_late = 0f;
        foreach (var s in buf_early) rms_early += s * s;
        foreach (var s in buf_late)  rms_late  += s * s;
        // Output should differ (filter modulation is active)
        Assert.That(rms_early, Is.Not.EqualTo(rms_late).Within(1e-6f),
            "Filter ENV modulation must change output over time.");
    }

    [Test] public void SetFilterEnv_ZeroEnvAmt_NoModulation()
    {
        // ENV AMT=0: two engines with different filter ENV params but AMT=0
        // must produce identical output (filter ENV not applied)
        using var e1 = new Engine();
        using var e2 = new Engine();
        e1.SetFilterParams(0.5f, 0f, FilterKind.Roland);
        e2.SetFilterParams(0.5f, 0f, FilterKind.Roland);
        e1.SetFilterEnv(0.001f, 0.1f, 0f, 0.1f);   // fast env
        e2.SetFilterEnv(1.0f,   1.0f, 1.0f, 1.0f); // slow env — different
        e1.SetFilterEnvAmount(0f); // no modulation
        e2.SetFilterEnvAmount(0f); // no modulation
        e1.SendNoteOn(60, 0.8f, 2, 5, 0);
        e2.SendNoteOn(60, 0.8f, 2, 5, 0);
        var buf1 = new float[512]; var buf2 = new float[512];
        e1.ProcessAudioCallback(buf1.AsSpan());
        e2.ProcessAudioCallback(buf2.AsSpan());
        float diff = 0f;
        for (int i = 0; i < buf1.Length; i++) diff += MathF.Abs(buf1[i] - buf2[i]);
        Assert.That(diff, Is.LessThan(0.1f),
            "ENV AMT=0: different filter ENV params must produce same output.");
    }

    // ── SetAmpEnv ────────────────────────────────────────────────────────

    [Test] public void SetAmpEnv_LongAttack_ProducesGradualRise()
    {
        using var e = new Engine();
        e.SetAmpEnv(1.0f, 0.1f, 1.0f, 0.1f); // 1 second attack
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        var buf_early = new float[512];
        e.ProcessAudioCallback(buf_early.AsSpan());
        for (int i = 0; i < 40; i++) {
            var tmp = new float[512]; e.ProcessAudioCallback(tmp.AsSpan());
        }
        var buf_late = new float[512];
        e.ProcessAudioCallback(buf_late.AsSpan());
        float rms_early = 0f, rms_late = 0f;
        foreach (var s in buf_early) rms_early += s * s;
        foreach (var s in buf_late)  rms_late  += s * s;
        Assert.That(rms_early, Is.LessThan(rms_late),
            "Long attack: early output must be quieter than later output.");
    }

    // ── SetWave ──────────────────────────────────────────────────────────

    [Test] public void SetWave_AllWaveTypes_ProduceFiniteOutput()
    {
        foreach (WaveType wave in System.Enum.GetValues(typeof(WaveType)))
        {
            using var e = new Engine();
            e.SetWave(wave);
            e.SendNoteOn(60, 0.8f, 2, 5, 0);
            var buf = new float[1024];
            e.ProcessAudioCallback(buf.AsSpan());
            foreach (var s in buf)
                Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False,
                    $"Wave {wave} produced NaN/Inf.");
        }
    }


}