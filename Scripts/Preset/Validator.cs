// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Preset;

/// <summary>Clamp all preset parameters to valid ranges on load.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public static class Validator {
    public static Preset Validate(Preset raw) {
        if (raw == null) return Preset.Default;
        return new Preset {
            name           = raw.name ?? "Unnamed",
            version        = raw.version ?? "1.0",
            osc1           = ValidateOsc(raw.osc1),
            osc2           = ValidateOsc(raw.osc2),
            filter         = ValidateFilter(raw.filter),
            amp_envelope    = ValidateEnvelope(raw.amp_envelope),
            filter_envelope = ValidateEnvelope(raw.filter_envelope),
            pitch_envelope  = ValidateEnvelope(raw.pitch_envelope),
            lfo1           = ValidateLFO(raw.lfo1),
            lfo2           = ValidateLFO(raw.lfo2),
            portamento_time = Clamp(raw.portamento_time, 0f, 5f),
            effects        = ValidateEffects(raw.effects),
            retro_mode      = raw.retro_mode,
        };
    }

    static OscPreset ValidateOsc(OscPreset? raw) {
        if (raw == null) return OscPreset.Default;
        return new OscPreset {
            wave        = raw.wave,
            interp      = raw.interp,
            detune_cents = Clamp(raw.detune_cents, -100f, 100f),
            pulse_width  = Clamp(raw.pulse_width, 0.01f, 0.99f),
            level       = Clamp(raw.level, 0f, 1f),
        };
    }

    static FilterPreset ValidateFilter(FilterPreset? raw) {
        if (raw == null) return FilterPreset.Default;
        return new FilterPreset {
            mode      = raw.mode,
            cutoff    = Clamp(raw.cutoff, 0.001f, 0.999f),
            resonance = Clamp(raw.resonance, 0f, 1f),
            env_amt    = Clamp(raw.env_amt, -1f, 1f),
            key_follow = Clamp(raw.key_follow, 0f, 1f),
        };
    }

    static EnvPreset ValidateEnvelope(EnvPreset? raw) {
        if (raw == null) return EnvPreset.Default;
        return new EnvPreset {
            attack  = Clamp(raw.attack,  0.001f, 10f),
            decay   = Clamp(raw.decay,   0.001f, 10f),
            sustain = Clamp(raw.sustain, 0f, 1f),
            release = Clamp(raw.release, 0.001f, 20f),
        };
    }

    static LfoPreset ValidateLFO(LfoPreset? raw) {
        if (raw == null) return LfoPreset.Default;
        return new LfoPreset {
            wave         = raw.wave,
            rate_or_sync   = Clamp(raw.rate_or_sync, 0.001f, 20f),
            depth        = Clamp(raw.depth, 0f, 1f),
            tempo_sync    = raw.tempo_sync,
            destinations = raw.destinations,
        };
    }

    static FxPreset ValidateEffects(FxPreset? raw) {
        if (raw == null) return FxPreset.Default;
        return new FxPreset {
            chorus_mode     = raw.chorus_mode,
            chorus_rate     = Clamp(raw.chorus_rate, 0f, 20f),
            chorus_depth    = Clamp(raw.chorus_depth, 0f, 1f),
            chorus_mix      = Clamp(raw.chorus_mix, 0f, 1f),
            reverb_room_size = Clamp(raw.reverb_room_size, 0f, 1f),
            reverb_damping  = Clamp(raw.reverb_damping, 0f, 1f),
            reverb_mix      = Clamp(raw.reverb_mix, 0f, 1f),
            delay_time      = Clamp(raw.delay_time, 0.001f, 2f),
            delay_feedback  = Clamp(raw.delay_feedback, 0f, 0.95f),
            delay_mix       = Clamp(raw.delay_mix, 0f, 1f),
            delay_tempo_sync = raw.delay_tempo_sync,
            retro_mode      = raw.retro_mode,
        };
    }

    // Custom clamp — Math.Clamp blocks SIMD due to NaN branch
    static float Clamp(float v, float min, float max) {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}
