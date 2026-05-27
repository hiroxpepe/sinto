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
        Assert.That(e.IsPaused, Is.False);
    }

    [Test] public void Pause_SetsPausedTrue()
    {
        using var e = new Engine();
        e.Pause();
        Assert.That(e.IsPaused, Is.True);
    }

    [Test] public void Resume_SetsPausedFalse()
    {
        using var e = new Engine();
        e.Pause();
        e.Resume();
        Assert.That(e.IsPaused, Is.False);
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
        Assert.That(e.CurrentBpm, Is.EqualTo(140f).Within(0.1f));
    }

    [Test] public void ActiveVoices_Initially_IsZero()
    {
        using var e = new Engine();
        Assert.That(e.ActiveVoices, Is.EqualTo(0));
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
        // Verify that processing a buffer with no events keeps ActiveVoices = 0.
        // The None event defense is: (1) default(Event).Kind == None proven above,
        // (2) ApplyEvent must have a case/default that discards None without triggering NoteOn.
        using var e = new Engine();
        var buf = new float[512];
        // Process empty buffer (ring buffer has no events = effectively None events)
        e.ProcessAudioCallback(buf.AsSpan());
        Assert.That(e.ActiveVoices, Is.EqualTo(0),
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
        Assert.That(e.ActiveVoices, Is.EqualTo(0),
            "1000 empty ProcessAudioCallback calls must not trigger any phantom NoteOn.");
    }

    [Test] public void Pause_Resume_TickDoesNotAccumulate()
    {
        // DoesNotThrow だけでは「蓄積していない」証明にならない。
        // DspTimeSamples が Pause 中に進んでいないことを数値で検証する。
        using var e = new Engine();
        var buf = new float[512];

        // 1バッファ分だけ再生して DspTime の基準値を得る
        e.ProcessAudioCallback(buf.AsSpan());
        long beforePause = e.DspTimeSamples;

        // Pause して 100バッファ分 ProcessAudioCallback を呼ぶ
        e.Pause();
        for (int i = 0; i < 100; i++) e.ProcessAudioCallback(buf.AsSpan());
        long duringPause = e.DspTimeSamples;

        // Pause 中は DspTime が進んでいないこと
        Assert.That(duringPause, Is.EqualTo(beforePause),
            "DspTimeSamples must NOT advance while paused.");

        // Resume して 1バッファ分だけ処理
        e.Resume();
        e.ProcessAudioCallback(buf.AsSpan());
        long afterResume = e.DspTimeSamples;

        // Resume 後は 1バッファ分だけ進んでいること（爆速蓄積していないこと）
        Assert.That(afterResume, Is.EqualTo(beforePause + 512),
            "DspTimeSamples must advance by exactly 1 buffer after Resume.");
    }
}
