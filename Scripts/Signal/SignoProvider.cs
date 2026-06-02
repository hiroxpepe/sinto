// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Signo.Core.Effects;
using Signo.Core.Synth;

namespace Signo.Core.Signal;

/// <summary>
/// Wires VAEngine → Channel → EffectBus in the correct signal flow.
/// Replaces the old pattern of calling FX methods directly on VAEngine.
///
/// Signal flow:
///   VAEngine (voice synthesis)
///     → Channel (insert FX: EQ/Comp/Flanger/Phaser/etc.)
///     → EffectBus (send-return: Chorus/Delay/Reverb)
/// </summary>
public sealed class SignoProvider
{
    readonly VAEngine _engine;
    readonly float[]  _send;

    public Channel   Channel   { get; }
    public EffectBus EffectBus { get; }

    // Current MOD insert FX slot (one at a time)
    IInsertEffect? _modFx;

    public SignoProvider(VAEngine engine, int bufferFrames = 1024)
    {
        _engine   = engine;
        Channel   = new Channel(44100);
        EffectBus = new EffectBus(44100, bufferFrames);
        _send     = new float[bufferFrames * 2];
    }

    // ── Note control ─────────────────────────────────────────────────
    public bool NoteOn(int midi, float velocity, int trackId, int priority, ushort offset = 0)
        => _engine.SendNoteOn(midi, velocity, trackId, priority, offset);

    public bool NoteOff(int midi, int trackId, ushort offset = 0)
        => _engine.SendNoteOff(midi, trackId, offset);

    // ── Audio processing ─────────────────────────────────────────────
    public int Process(float[] buffer, int offset, int count, int channels)
    {
        var span = buffer.AsSpan(offset, count);
        // 1. Voice synthesis
        _engine.ProcessAudioCallback(span);
        // 2. Channel insert FX
        Channel.Process(span, channels);
        // 3. Send-return via EffectBus
        int n = Math.Min(count, _send.Length);
        span.Slice(0, n).CopyTo(_send.AsSpan(0, n));
        EffectBus.ProcessSend(span, _send, Channel.ReverbSend, Channel.DelaySend, Channel.ChorusSend);
        return count;
    }

    // ── FX send amounts ───────────────────────────────────────────────
    public void SetChorusSend(float amount) => Channel.ChorusSend = Math.Clamp(amount, 0f, 1f);
    public void SetDelaySend(float amount)  => Channel.DelaySend  = Math.Clamp(amount, 0f, 1f);
    public void SetReverbSend(float amount) => Channel.ReverbSend = Math.Clamp(amount, 0f, 1f);

    // ── MOD insert FX (one slot: Flanger/Phaser/Tremolo/Vibrato/AutoWah/Chorus) ──
    public void SetModFx(ChorusType type, float rate, float depth, float param3, float send)
    {
        // Remove previous MOD FX
        if (_modFx != null) { Channel.RemoveInsert(_modFx); _modFx = null; }
        if (send <= 0f) return;

        IInsertEffect fx;
        switch (type) {
            case ChorusType.Flanger: {
                var f = new Flanger(44100);
                f.SetParams(rate, depth, param3, send);
                fx = f; break;
            }
            case ChorusType.Phaser: {
                var p = new Phaser(44100);
                p.SetParams(rate, depth, param3, send);
                fx = p; break;
            }
            case ChorusType.Tremolo: {
                var t = new Tremolo(44100);
                t.SetParams(rate, depth);
                t.Send = send;
                fx = t; break;
            }
            case ChorusType.Vibrato: {
                var v = new Vibrato(44100);
                v.SetParams(rate, depth);
                v.Send = send;
                fx = v; break;
            }
            case ChorusType.AutoWah: {
                var w = new AutoWah(44100);
                w.SetParams(rate, depth, param3);
                w.Send = send;
                fx = w; break;
            }
            default: return; // Chorus goes through EffectBus
        }
        fx.enabled = true;
        Channel.AddInsert(fx);
        _modFx = fx;
        _engine.SetChorusType(type);
    }

    // ── EffectBus params ─────────────────────────────────────────────
    public void SetChorusParams(float rate, float depth) {
        EffectBus.Chorus.rate  = rate;
        EffectBus.Chorus.depth = depth;
        EffectBus.Chorus.enabled = true;
    }
    public void SetDelayParams(float timeSec, float feedback) {
        EffectBus.Delay.time     = timeSec;
        EffectBus.Delay.feedback = feedback;
        EffectBus.Delay.enabled  = true;
    }
    public void SetReverbParams(float roomSize, float damping) {
        EffectBus.Reverb.roomSize = roomSize;
        EffectBus.Reverb.damping  = damping;
        EffectBus.Reverb.enabled  = true;
    }
    public void SetDelayBpmSync(float bpm, NoteValue note)
        => EffectBus.Delay.time = note.ToMs(bpm) / 1000f;
    public void SetFlangerBpmSync(float bpm, NoteValue note) {
        if (_modFx is Flanger f) f.SetBpmSync(bpm, note);
    }
    public void SetFlangerLfoWaveform(LfoWaveform w) {
        if (_modFx is Flanger f) f.SetLfoWaveform(w);
    }
    public void SetFlangerStepMode(FlangerStepMode m) {
        if (_modFx is Flanger f) f.SetStepMode(m);
    }
    public void SetPhaserBpmSync(float bpm, NoteValue note) {
        if (_modFx is Phaser p) p.SetBpmSync(bpm, note);
    }
    public void SetModFxPhaser(float rate, float depth, float res, float send) {
        SetModFx(ChorusType.Phaser, rate, depth, res, send);
    }
    public void SetPhaserLfoWaveform(LfoWaveform w) {
        if (_modFx is Phaser p) p.SetLfoWaveform(w);
    }
    public void SetTremoloWaveform(TremoloWaveform w) {
        if (_modFx is Tremolo t) t.SetWaveform(w);
    }
    public void SetAutoWahMode(WahMode m) {
        if (_modFx is AutoWah a) a.SetMode(m);
    }
}

// Extension for test inspection
public static class ChannelExtensions
{
    public static bool HasInsert<T>(this Channel ch) where T : IInsertEffect
    {
        var field = typeof(Channel).GetField("_inserts",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(ch) is System.Collections.Generic.List<IInsertEffect> list)
            return list.Exists(fx => fx is T);
        return false;
    }
}
