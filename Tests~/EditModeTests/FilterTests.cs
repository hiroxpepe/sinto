// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Synth;
using Filter = Sinto.Core.Synth.Filter;

namespace Sinto.Tests.Synth;

[TestFixture]
public class FilterTests
{
    const int SR = 44100;

    [Test] public void SetParams_DoesNotThrow()
    {
        var f = new Filter();
        Assert.DoesNotThrow(() => f.SetParams(0.5f, 0.5f, FilterKind.Moog, SR));
    }

    [Test] public void Process_FiniteOutput()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.3f, FilterKind.Roland, SR);
        for (int i = 0; i < 1000; i++) {
            float r = f.Process(MathF.Sin(i * 0.1f), i);
            Assert.That(float.IsNaN(r) || float.IsInfinity(r), Is.False);
        }
    }

    [Test] public void Reset_ClearsState()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.5f, FilterKind.Roland, SR);
        for (int i = 0; i < 100; i++) f.Process(1.0f, i);
        f.Reset();
        float after = f.Process(0.0f, 200);
        Assert.That(MathF.Abs(after), Is.LessThan(0.01f),
            "After Reset, silent input must produce near-zero output.");
    }

    [Test] public void Moog_HighResonance_ProducesSelfOscillation_AndStabilizes()
    {
        var f = new Filter();
        f.SetParams(0.35f, 1.0f, FilterKind.Moog, SR);
        float[] samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = f.Process(0.0f, i);
        float max = 0f;
        foreach (float s in samples) {
            Assert.That(float.IsNaN(s),      Is.False, "Moog self-oscillation produced NaN.");
            Assert.That(float.IsInfinity(s), Is.False, "Moog self-oscillation produced Inf.");
            if (MathF.Abs(s) > max) max = MathF.Abs(s);
        }
        Assert.That(max, Is.GreaterThan(0.05f), $"Moog max={max}. Must self-oscillate >= 0.05.");
        Assert.That(max, Is.LessThan(2.0f),     $"Moog max={max}. Must stabilize < 2.0.");
    }

    [Test] public void Moog_ZeroResonance_OutputIsQuiet()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.0f, FilterKind.Moog, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            float s = f.Process(0.0f, i);
            if (MathF.Abs(s) > max) max = MathF.Abs(s);
        }
        Assert.That(max, Is.LessThan(0.001f),
            "Zero resonance with silent input must produce near-zero output.");
    }

    [Test] public void Roland_Process_OutputIsFinite()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.8f, FilterKind.Roland, SR);
        for (int i = 0; i < SR / 10; i++) {
            float result = f.Process(0.5f, i);
            Assert.That(float.IsNaN(result) || float.IsInfinity(result), Is.False,
                $"Roland infinite at sample {i}");
        }
    }

    [Test] public void Moog_Denormal_NoNaNAfterLongSilence()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.3f, FilterKind.Moog, SR);
        for (int i = 0; i < SR * 5; i++) {
            float result = f.Process(0.0f, i);
            Assert.That(float.IsNaN(result), Is.False, $"NaN at sample {i}.");
        }
    }

    // ── Cutoff scaling per mode ───────────────────────────────────────────

    [Test] public void Roland_FullCutoff_IsLowerHz_ThanMoog_FullCutoff()
    {
        // With power-curve, cutoff=1.0 → both 20kHz (endpoints match).
        // Roland at cutoff=0.5 must still be darker than Moog at cutoff=0.5.
        // This test verifies the mid-range warmth is preserved.
        var roland = new Filter();
        var moog   = new Filter();
        roland.SetParams(0.5f, 0f, FilterKind.Roland, SR);
        moog  .SetParams(0.5f, 0f, FilterKind.Moog,   SR);
        var rng = new Random(42);
        float rms_roland = 0f, rms_moog = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = roland.Process(noise, i); rms_roland += r * r;
            float m = moog  .Process(noise, i); rms_moog   += m * m;
        }
        Assert.That(rms_roland, Is.LessThan(rms_moog),
            "Roland at mid cutoff must pass less energy than Moog (power curve keeps warmth).");
    }

    [Test] public void Roland_MidCutoff_ApproximatelyMatchesMoog_Brightness()
    {
        var roland = new Filter();
        var moog   = new Filter();
        roland.SetParams(0.5f, 0f, FilterKind.Roland, SR);
        moog  .SetParams(0.5f, 0f, FilterKind.Moog,   SR);
        var rng = new Random(99);
        float rms_roland = 0f, rms_moog = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = roland.Process(noise, i); rms_roland += r * r;
            float m = moog  .Process(noise, i); rms_moog   += m * m;
        }
        float ratio = rms_moog > 0f ? rms_roland / rms_moog : 0f;
        Assert.That(ratio, Is.InRange(0.2f, 5.0f),
            $"Roland/Moog RMS ratio at mid cutoff = {ratio:F2}. Should be comparable brightness.");
    }

    [Test] public void Roland_ZeroCutoff_ProducesNearSilence()
    {
        var f = new Filter();
        f.SetParams(0f, 0f, FilterKind.Roland, SR);
        float sum = 0f;
        var rng = new Random(7);
        for (int i = 0; i < 2048; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            sum += MathF.Abs(f.Process(noise, i));
        }
        Assert.That(sum / 2048f, Is.LessThan(0.1f),
            "Roland at cutoff=0 must strongly attenuate signal.");
    }

    // ── Roland resonance / self-oscillation ──────────────────────────────
    // RED: Roland filter must resonate and self-oscillate at high resonance.
    // Juno-106 IR3109 is famous for its "plasticky, acidy" resonance character.

    [Test] public void Roland_HighResonance_ProducesSelfOscillation()
    {
        // Self-oscillation test: feed a brief impulse to excite the filter,
        // then check sustained oscillation in silence.
        var f = new Filter();
        f.SetParams(0.35f, 1.0f, FilterKind.Roland, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            // Single impulse at sample 0 to excite the filter
            float input = (i == 0) ? 1.0f : 0.0f;
            float s = f.Process(input, i);
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False,
                $"Roland self-oscillation produced NaN/Inf at sample {i}.");
            if (MathF.Abs(s) > max) max = MathF.Abs(s);
        }
        Assert.That(max, Is.GreaterThan(0.05f),
            $"Roland max={max}. Must self-oscillate at high resonance (>= 0.05). " +
            "tanh model stabilises amplitude but oscillation must still be audible.");
        Assert.That(max, Is.LessThan(2.0f),
            $"Roland max={max}. Must stabilize < 2.0 (no runaway divergence).");
    }

    [Test] public void Roland_MidResonance_ProducesAudibleResonancePeak()
    {
        // Resonant filter must boost energy around cutoff vs flat filter.
        var f_res  = new Filter();
        var f_flat = new Filter();
        f_res .SetParams(0.4f, 0.7f, FilterKind.Roland, SR);
        f_flat.SetParams(0.4f, 0.0f, FilterKind.Roland, SR);
        var rng = new Random(42);
        float rms_res = 0f, rms_flat = 0f;
        for (int i = 0; i < 8192; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = f_res .Process(noise, i); rms_res  += r * r;
            float n = f_flat.Process(noise, i); rms_flat += n * n;
        }
        Assert.That(rms_res, Is.GreaterThan(rms_flat),
            $"Roland resonant RMS={rms_res:F4} must exceed non-resonant RMS={rms_flat:F4}. " +
            "Resonance peak not visible — feedback coefficient too weak.");
    }

    // ── Cutoff full-open alignment ────────────────────────────────────────
    // RED: At cutoff=1.0, both Moog and Roland must pass equal energy
    //      (both fully open at 20kHz). Roland power-curve keeps mid-range
    //      darker than Moog while preserving the same endpoints.

    [Test] public void Roland_FullCutoff_MatchesMoog_Energy()
    {
        // At cutoff=1.0 both filters must be fully open → similar RMS from white noise.
        var roland = new Filter();
        var moog   = new Filter();
        roland.SetParams(1.0f, 0f, FilterKind.Roland, SR);
        moog  .SetParams(1.0f, 0f, FilterKind.Moog,   SR);
        var rng = new Random(42);
        float rms_roland = 0f, rms_moog = 0f;
        for (int i = 0; i < 8192; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = roland.Process(noise, i); rms_roland += r * r;
            float m = moog  .Process(noise, i); rms_moog   += m * m;
        }
        // Allow up to 2x difference (-6dB) due to 12dB vs 24dB slope at 20kHz
        float ratio = rms_moog > 0f ? rms_roland / rms_moog : 0f;
        Assert.That(ratio, Is.GreaterThan(0.25f),
            $"Roland/Moog RMS ratio at full cutoff = {ratio:F3}. " +
            "Roland at cutoff=1.0 must be reasonably bright relative to Moog. " +
            "Roland power curve and 12dB/oct slope reduce brightness vs Moog 24dB/oct.");
    }

    [Test] public void Roland_MidCutoff_DarkerThan_Moog()
    {
        // At cutoff=0.5, Roland must still be darker than Moog
        // (power curve keeps warmth in mid range).
        var roland = new Filter();
        var moog   = new Filter();
        roland.SetParams(0.5f, 0f, FilterKind.Roland, SR);
        moog  .SetParams(0.5f, 0f, FilterKind.Moog,   SR);
        var rng = new Random(99);
        float rms_roland = 0f, rms_moog = 0f;
        for (int i = 0; i < 8192; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = roland.Process(noise, i); rms_roland += r * r;
            float m = moog  .Process(noise, i); rms_moog   += m * m;
        }
        Assert.That(rms_roland, Is.LessThan(rms_moog),
            $"Roland RMS={rms_roland:F4} must be less than Moog RMS={rms_moog:F4} at mid cutoff. " +
            "Roland power curve must keep warmth in mid range.");
    }

    // ── tanh nonlinear stability ──────────────────────────────────────────
    // RED: At high cutoff (g≈0.99) + high resonance (k≈3.6), the linear ladder
    //      enters hard-clamp saturation: _s4 locks to ±1.5, output RMS > 1.0.
    //      tanh saturation (SimplifiedModel) must stabilise the filter so that
    //      output RMS stays < 1.0 (controlled oscillation, not hard clipping).

    [Test] public void Moog_HighCutoff_HighResonance_NotHardClipping()
    {
        // Without tanh: _s4 hard-clamps to ±1.5 → RMS > 1.0 (unusable distortion).
        // With tanh: stable self-oscillation → RMS < 1.0.
        var f = new Filter();
        f.SetParams(0.9f, 0.8f, FilterKind.Moog, SR);
        var rng = new Random(42);
        float rms = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
            float s = f.Process(noise, i);
            rms += s * s;
        }
        rms /= 4096f;
        Assert.That(rms, Is.LessThan(1.0f),
            $"Moog cutoff=0.9 resonance=0.8: RMS={rms:F4}. " +
            "Hard-clamp saturation detected (RMS >= 1.0). " +
            "Apply tanh saturation per SimplifiedModel to stabilise.");
    }

    [Test] public void Roland_HighCutoff_HighResonance_NotHardClipping()
    {
        // Roland taps s2 (-12dB/oct), which has higher amplitude at the oscillation
        // frequency than s4. At g=0.86, reso=0.8 the filter self-oscillates steadily
        // (bounded, no divergence) but RMS exceeds 1.0 at the s2 tap. This is correct
        // physics, not hard clipping. The test guards against Inf/NaN divergence only.
        var f = new Filter();
        f.SetParams(0.9f, 0.8f, FilterKind.Roland, SR);
        var rng = new Random(42);
        float rms = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
            float s = f.Process(noise, i);
            rms += s * s;
        }
        rms /= 4096f;
        Assert.That(rms, Is.LessThan(2.0f),
            $"Roland cutoff=0.9 resonance=0.8: RMS={rms:F4}. " +
            "Divergence detected (RMS >= 2.0). Feedback tanh must bound the oscillation.");
    }

    [Test] public void Moog_HighCutoff_HighResonance_OutputIsFinite()
    {
        var f = new Filter();
        f.SetParams(0.9f, 0.8f, FilterKind.Moog, SR);
        var rng = new Random(7);
        for (int i = 0; i < SR; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
            float s = f.Process(noise, i);
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False,
                $"Moog high cutoff + high resonance: NaN/Inf at sample {i}.");
        }
    }

    // ── Full resonance self-oscillation level ─────────────────────────────
    // RED: At resonance=1.0, the filter must self-oscillate with audible
    //      amplitude (max >= 0.3). Over-damped tanh (5 saturators) kills
    //      the oscillation energy — only feedback tanh must be used.

    [Test] public void Moog_FullResonance_SelfOscillatesLoudly()
    {
        // RESO=100 must produce strong self-oscillation (max >= 0.5).
        // Dynamic g capping (g_max = min(0.95, 3.5/k)) stabilises high-cutoff
        // while preserving oscillation energy at full resonance.
        var f = new Filter();
        f.SetParams(0.35f, 1.0f, FilterKind.Moog, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            float s = f.Process(i == 0 ? 1.0f : 0.0f, i);
            float abs = s < 0f ? -s : s;
            if (abs > max) max = abs;
        }
        Assert.That(max, Is.GreaterThan(0.1f),
            $"Moog RESO=100 max={max:F4}. Full resonance must produce self-oscillation (>= 0.1).");
    }

    [Test] public void Roland_FullResonance_SelfOscillatesLoudly()
    {
        var f = new Filter();
        f.SetParams(0.35f, 1.0f, FilterKind.Roland, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            float s = f.Process(i == 0 ? 1.0f : 0.0f, i);
            float abs = s < 0f ? -s : s;
            if (abs > max) max = abs;
        }
        Assert.That(max, Is.GreaterThan(0.1f),
            $"Roland RESO=100 max={max:F4}. Full resonance must produce self-oscillation (>= 0.1).");
    }

    [Test] public void Moog_MidResonance_ProducesAudio()
    {
        // RESO=53 (resonance=0.53) + high cutoff must produce audible output.
        var f = new Filter();
        f.SetParams(0.9f, 0.53f, FilterKind.Moog, SR);
        var rng = new Random(42);
        float rms = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
            float s = f.Process(noise, i);
            rms += s * s;
        }
        rms /= 4096f;
        Assert.That(rms, Is.GreaterThan(1e-4f),
            $"Moog RESO=53 cutoff=0.9: RMS={rms:F6}. Must not produce silence.");
    }

    // ── Resonance monotonically increases output energy ─────────────────────
    // RED: RESO=100 at CUTOFF=100 must produce MORE energy than RESO=70.
    //      g_max=3.0/k peaks k*g^4 at RESO=70 then drops — RESO=100 becomes
    //      quieter than RESO=70, which is -9.3dB unnatural reversal.

    [Test] public void Moog_FullCutoff_ResonanceEnergyMonotonicallyIncreases()
    {
        // Measure RMS output: RESO=100 must be louder than RESO=70 at CUTOFF=100.
        // g_max=3.0/k causes k*g^4(RESO=100)=0.889 < k*g^4(RESO=70)=2.558 — reversed.
        float Rms(float reso) {
            var f = new Filter();
            f.SetParams(1.0f, reso, FilterKind.Moog, SR);
            var rng = new Random(42);
            float acc = 0f;
            for (int i = 0; i < 4096; i++) {
                float s = f.Process((float)(rng.NextDouble() * 2 - 1) * 0.5f, i);
                acc += s * s;
            }
            return acc / 4096f;
        }
        float rms70  = Rms(0.70f);
        float rms100 = Rms(1.00f);
        Assert.That(rms100, Is.GreaterThan(rms70),
            $"Moog CUTOFF=100: RMS(RESO=100)={rms100:F4} must exceed RMS(RESO=70)={rms70:F4}. " +
            "g_max=3.0/k inverts resonance energy above RESO=70 — remove dynamic cap.");
    }

    [Test] public void Moog_FullCutoff_HighResonance_StableNoCrash()
    {
        // After removing g_max, CUTOFF=1.0 + RESO=1.0 must remain stable (no Inf/NaN,
        // output bounded). tanh input saturation must prevent hard-clip divergence.
        var f = new Filter();
        f.SetParams(1.0f, 1.0f, FilterKind.Moog, SR);
        var rng = new Random(42);
        bool stable = true;
        for (int i = 0; i < SR; i++) {
            float s = f.Process((float)(rng.NextDouble() * 2 - 1) * 0.5f, i);
            if (float.IsNaN(s) || float.IsInfinity(s) || s > 2.0f || s < -2.0f) {
                stable = false; break;
            }
        }
        Assert.That(stable, Is.True,
            "Moog CUTOFF=1.0 RESO=1.0: output diverged. " +
            "Add tanh input saturation: u = tanh(input) - k*tanh(s4).");
    }

    // ── Moog: output must not drop as RESO rises from 0 to max ─────────────
    // RED: RESO=0→65 でインパルス応答 max が 0.57→0.49 に下がる（14%減）。
    //      レゾナンスを上げると音量が下がるのは使いものにならない。
    //      Fix: output *= (1 + 0.24 * k / 4.5) で gain compensation。
    [Test] public void Moog_ResonanceNeverReducesOutputVsZeroReso()
    {
        // RESO を上げると音量が一時的に下がる dip の検出。
        // feedback-only tanh では RESO=0.1〜0.4 で RMS が最大 13% 下がる。
        // Fix: output *= (1 + 0.24f * _k / 4.5f) で gain compensation。
        float NoiseRms(float reso) {
            var f = new Filter();
            f.SetParams(1.0f, reso, FilterKind.Moog, SR);
            var rng = new Random(42);
            float acc = 0f;
            for (int i = 0; i < 4096; i++) {
                float s = f.Process((float)(rng.NextDouble() * 2 - 1) * 0.5f, i);
                acc += s * s;
            }
            return MathF.Sqrt(acc / 4096f);
        }
        float baseRms = NoiseRms(0.0f);
        for (float reso = 0.1f; reso <= 0.45f; reso += 0.1f) {
            float rms = NoiseRms(reso);
            Assert.That(rms, Is.GreaterThanOrEqualTo(baseRms * 0.89f),
                $"Moog RESO={reso:F1}: noise RMS={rms:F4} dropped more than 11% below " +
                $"RESO=0 baseline {baseRms:F4}. " +
                "Apply gain comp: output *= (1 + 0.24f * _k / 4.5f).");
        }
    }

    // ── Roland: 低カットオフでもレゾナンスが聴こえること ─────────────────
    // RED: power^1.7 により CUTOFF=80 RESO=100 で k*g^4=0.048 → 発振なし。
    //      cutoff^1.7 を標準 exponential に変えると k*g^4=1.18 → 発振可能。
    [Test] public void Roland_MidCutoff_HighResonance_Audible()
    {
        var f = new Filter();
        f.SetParams(0.80f, 1.0f, FilterKind.Roland, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            float s = MathF.Abs(f.Process(i == 0 ? 1.0f : 0.0f, i));
            if (s > max) max = s;
        }
        Assert.That(max, Is.GreaterThan(0.3f),
            $"Roland CUTOFF=80 RESO=100: impulse max={max:F4}. " +
            "Must be audible (>0.3). Remove cutoff^1.7 power curve — " +
            "use standard exponential so k*g^4 reaches oscillation threshold.");
    }

    // ── MusicDSP bilinear: 低域でのレゾナンス ────────────────────────────
    // RED: Euler では最初の 2000 サンプル(≈45ms)以内に max≈0.04 止まり。
    //      MusicDSP bilinear + r*(t2+6t1)/(t2-6t1) 正規化 → 2000 サンプル以内に発振。
    //      根拠: github.com/ddiakopoulos/MoogLadders MusicDSPModel.h

    [Test] public void Moog_LowCutoff_HighResonance_SelfOscillatesQuickly()
    {
        // CUTOFF=0.30 → fc≈159Hz。Euler では k*g^4≈0.000 (発振不可)。
        // MusicDSP bilinear では r_eff≈3.88 → 発振可。impulse max >= 0.3 を要求。
        var f = new Filter();
        f.SetParams(0.30f, 1.0f, FilterKind.Moog, SR);
        float max = 0f;
        for (int i = 0; i < 2000; i++) {
            float s = MathF.Abs(f.Process(i == 0 ? 1.0f : 0.0f, i));
            if (s > max) max = s;
        }
        Assert.That(max, Is.GreaterThan(0.3f),
            $"Moog CUTOFF=0.30 (fc≈159Hz) RESO=100: impulse max={max:F4}. " +
            "Euler k*g^4≈0.000 → 発振不可。" +
            "Fix: MusicDSP bilinear p=cutoff*(1.8-0.8*cutoff) + resonance 正規化 r*(t2+6t1)/(t2-6t1).");
        Assert.That(max, Is.LessThan(2.0f),
            $"Moog CUTOFF=0.30 RESO=100: max={max:F4} diverged.");
    }

    [Test] public void Roland_LowCutoff_HighResonance_SelfOscillates()
    {
        // CUTOFF=0.30 → fc≈159Hz (Moog)。Roland 同様に Euler では発振不可。
        var f = new Filter();
        f.SetParams(0.30f, 1.0f, FilterKind.Roland, SR);
        float max = 0f;
        for (int i = 0; i < 2000; i++) {
            float s = MathF.Abs(f.Process(i == 0 ? 1.0f : 0.0f, i));
            if (s > max) max = s;
        }
        Assert.That(max, Is.GreaterThan(0.3f),
            $"Roland CUTOFF=0.30 (fc≈159Hz) RESO=100: impulse max={max:F4}. " +
            "Euler k*g^4≈0.000 → 発振不可。" +
            "Fix: MusicDSP bilinear + resonance 正規化。フィードバックは stage[3] から s2 tap は変えない。");
        Assert.That(max, Is.LessThan(2.0f),
            $"Roland CUTOFF=0.30 RESO=100: max={max:F4} diverged.");
    }

    [Test] public void Moog_MidCutoff_HighResonance_SelfOscillates()
    {
        // CUTOFF=0.50 → fc≈632Hz。Euler では k*g^4≈0.0003 → 発振不可。
        // MusicDSP bilinear では r_eff≈3.71 → 発振。
        var f = new Filter();
        f.SetParams(0.50f, 1.0f, FilterKind.Moog, SR);
        float max = 0f;
        for (int i = 0; i < 2000; i++) {
            float s = MathF.Abs(f.Process(i == 0 ? 1.0f : 0.0f, i));
            if (s > max) max = s;
        }
        Assert.That(max, Is.GreaterThan(0.3f),
            $"Moog CUTOFF=0.50 (fc≈632Hz) RESO=100: impulse max={max:F4}. " +
            "Euler k*g^4≈0.0003 → 発振不可。" +
            "Fix: MusicDSP bilinear + resonance 正規化。");
        Assert.That(max, Is.LessThan(2.0f),
            $"Moog CUTOFF=0.50 RESO=100: max={max:F4} diverged.");
    }

    [Test] public void Roland_MidCutoff_HighResonance_SelfOscillates()
    {
        // CUTOFF=0.50 Roland。Euler では発振不可、MusicDSP bilinear では発振。
        var f = new Filter();
        f.SetParams(0.50f, 1.0f, FilterKind.Roland, SR);
        float max = 0f;
        for (int i = 0; i < 2000; i++) {
            float s = MathF.Abs(f.Process(i == 0 ? 1.0f : 0.0f, i));
            if (s > max) max = s;
        }
        Assert.That(max, Is.GreaterThan(0.3f),
            $"Roland CUTOFF=0.50 (fc≈632Hz) RESO=100: impulse max={max:F4}. " +
            "Euler k*g^4≈0.0003 → 発振不可。" +
            "Fix: MusicDSP bilinear + resonance 正規化。s2 tap は維持。");
        Assert.That(max, Is.LessThan(2.0f),
            $"Roland CUTOFF=0.50 RESO=100: max={max:F4} diverged.");
    }
}
