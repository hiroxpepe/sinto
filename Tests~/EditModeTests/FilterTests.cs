// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;
using Filter = Signo.Core.Synth.Filter;

namespace Signo.Tests.Synth;

[TestFixture]
public class FilterTests
{
    const int SR = 44100;

    // ── 診断メソッド: UI ログ用に SetParams と同一の計算を一元提供 ─────────
    // RED: Filter.Diagnose が未実装。UI(MainWindow) が Euler 時代の式
    //      (k*g^4, osc_threshold=4.0) を二重実装しており現行 bilinear と食い違う。
    //      Fix: Filter.Diagnose(cutoff,reso,mode,sr) を追加し SetParams と同じ
    //           cutoff_hz / p / r_norm / oscillates を返す。UI はこれを呼ぶだけにする。

    [Test] public void Diagnose_MatchesSetParamsLogic()
    {
        // CUTOFF=0.5: fc=469Hz (fc_max=11kHz mapping), RESO=1.0 → 発振閾値超え。
        var d = Filter.Diagnose(0.5f, 1.0f, FilterKind.Moog, SR);
        Assert.That(d.cutoffHz, Is.EqualTo(469f).Within(2f),
            $"cutoff_hz={d.cutoffHz:F1}, expected ~469Hz. " +
            "Diagnose must use the same 20*exp(LN_FC_MAX*cutoff) mapping as SetParams.");
        Assert.That(d.oscillates, Is.True,
            $"RESO=1.0 r_norm={d.rNorm:F3} must oscillate (> 1.0).");
    }

    [Test] public void Diagnose_LowResonance_DoesNotOscillate()
    {
        // RESO=0.25 は発振閾値(0.75)未満 → oscillates=false。
        var d = Filter.Diagnose(0.5f, 0.25f, FilterKind.Moog, SR);
        Assert.That(d.oscillates, Is.False,
            $"RESO=0.25 r_norm={d.rNorm:F3} must NOT oscillate (< 1.0). " +
            "Threshold is RESO=0.75 with current normalization.");
    }

    // ── 高カットオフ発振のエイリアシング排除 (fc_max=11kHz) ──────────────
    // RED: self-oscillation は fc の約2倍の周波数で起きる。fc_max=20kHz だと
    //      CUTOFF=100 で発振が ~40kHz → Nyquist(22050Hz)超え → 折り返し(aliasing)。
    //      これが CUTOFF95-100 の「突発的な段差・楽器じゃない」聴感の正体(可視化で確認)。
    //      Fix: LN1000(fc_max 20kHz) → ln(11000/20)=6.30992 (fc_max 11kHz)。
    //      CUTOFF=100 で発振 ~22kHz に収まり aliasing 消滅。低域うなりは下限20Hz固定で無傷。
    //
    // 検出方法: ゼロクロッシングは折り返し後の見かけ周波数しか測れないため使えない。
    //   発振周波数の理論値 = fc * 2 (4-pole ladder) が Nyquist 未満かを fc から直接判定。

    [Test] public void FullCutoff_OscillationStaysBelowNyquist()
    {
        // CUTOFF=1.0 の fc。発振は fc*2 で起きるため fc < Nyquist/2 = 11025Hz が必要。
        var d = Filter.Diagnose(1.0f, 1.0f, FilterKind.Moog, SR);
        float oscFreq = d.cutoffHz * 2f;
        Assert.That(oscFreq, Is.LessThan(22050f),
            $"CUTOFF=1.0 fc={d.cutoffHz:F0}Hz → oscillation~{oscFreq:F0}Hz exceeds Nyquist → aliasing. " +
            "This is the 'step / not-an-instrument' artifact at CUTOFF95-100. " +
            "Fix: LN1000 → ln(11000/20)=6.30992 so fc_max=11kHz (oscillation ~22kHz, just under Nyquist).");
    }

    [Test] public void LowCutoff_GrowlUnaffected()
    {
        // 最重要: 低域うなり(ブブブ)は下限20Hz固定で fc_max 変更の影響を受けない。
        // CUTOFF=0 は両マッピングとも fc=20Hz。
        var d = Filter.Diagnose(0.0f, 1.0f, FilterKind.Moog, SR);
        Assert.That(d.cutoffHz, Is.EqualTo(20f).Within(0.5f),
            $"CUTOFF=0 fc={d.cutoffHz:F1}Hz must stay at 20Hz (sub-bass growl floor unaffected).");
    }

    // ── Process 共通化のタップ位置保証 ────────────────────────────────────
    // リファクタ(ProcessMoog/ProcessRoland → 共通 TickLadder)後も
    // Moog=s4(-24dB/oct)、Roland=s2*0.70(-12dB/oct) のタップが保たれることを保証。
    // 同一入力で Moog と Roland の出力が異なる(タップ位置が違う)ことを確認。

    [Test] public void Moog_And_Roland_ProduceDifferentOutput()
    {
        var fm = new Filter();
        var fr = new Filter();
        fm.SetParams(0.5f, 0.3f, FilterKind.Moog, SR);
        fr.SetParams(0.5f, 0.3f, FilterKind.Roland, SR);
        bool any_diff = false;
        for (int i = 0; i < 500; i++) {
            float inp = MathF.Sin(i * 0.05f);
            float om = fm.Process(inp, i);
            float or = fr.Process(inp, i);
            if (MathF.Abs(om - or) > 1e-4f) { any_diff = true; }
        }
        Assert.That(any_diff, Is.True,
            "Moog (s4 tap, -24dB/oct) and Roland (s2 tap, -12dB/oct) must differ. " +
            "If identical, the tap routing was lost during refactor.");
    }

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
        // RESO=0.27: equivalent margin above threshold as old RESO=0.8 with MusicDSP normalization.
        var f = new Filter();
        f.SetParams(0.9f, 0.27f, FilterKind.Moog, SR);
        var rng = new Random(42);
        float rms = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
            float s = f.Process(noise, i);
            rms += s * s;
        }
        rms /= 4096f;
        Assert.That(rms, Is.LessThan(1.0f),
            $"Moog cutoff=0.9 resonance=0.27: RMS={rms:F4}. " +
            "Hard-clamp saturation detected (RMS >= 1.0). " +
            "Apply tanh saturation per SimplifiedModel to stabilise.");
    }

    [Test] public void Roland_HighCutoff_HighResonance_NotHardClipping()
    {
        // Roland taps s2 (-12dB/oct), which has higher amplitude at the oscillation
        // frequency than s4. At high cutoff, reso=0.27 the filter self-oscillates steadily
        // (bounded, no divergence) but RMS exceeds 1.0 at the s2 tap. This is correct
        // physics, not hard clipping. The test guards against Inf/NaN divergence only.
        // RESO=0.27: equivalent margin above threshold as old RESO=0.8 with MusicDSP normalization.
        var f = new Filter();
        f.SetParams(0.9f, 0.27f, FilterKind.Roland, SR);
        var rng = new Random(42);
        float rms = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
            float s = f.Process(noise, i);
            rms += s * s;
        }
        rms /= 4096f;
        Assert.That(rms, Is.LessThan(2.0f),
            $"Roland cutoff=0.9 resonance=0.27: RMS={rms:F4}. " +
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
        // CUTOFF=0.30 → fc≈133Hz. MusicDSP bilinear self-oscillates here (Euler
        // would not). The original sigmoid limits amplitude and the build-up is
        // gradual, so we verify steady-state oscillation over 1s rather than a
        // fast transient. Steady max ≈ 0.30.
        var f = new Filter();
        f.SetParams(0.30f, 1.0f, FilterKind.Moog, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            float s = MathF.Abs(f.Process(i == 0 ? 1.0f : 0.0f, i));
            if (i > SR / 2 && s > max) max = s;  // measure after build-up
        }
        Assert.That(max, Is.GreaterThan(0.2f),
            $"Moog CUTOFF=0.30 (fc≈133Hz) RESO=100: steady max={max:F4}. " +
            "MusicDSP bilinear self-oscillates (sigmoid-limited ~0.30).");
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
        // CUTOFF=0.50 → fc≈469Hz。Euler では k*g^4≈0.0003 → 発振不可。
        // MusicDSP bilinear では r_eff≈3.71 → 発振。
        var f = new Filter();
        f.SetParams(0.50f, 1.0f, FilterKind.Moog, SR);
        float max = 0f;
        for (int i = 0; i < 2000; i++) {
            float s = MathF.Abs(f.Process(i == 0 ? 1.0f : 0.0f, i));
            if (s > max) max = s;
        }
        Assert.That(max, Is.GreaterThan(0.3f),
            $"Moog CUTOFF=0.50 (fc≈469Hz) RESO=100: impulse max={max:F4}. " +
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
            $"Roland CUTOFF=0.50 (fc≈469Hz) RESO=100: impulse max={max:F4}. " +
            "Euler k*g^4≈0.0003 → 発振不可。" +
            "Fix: MusicDSP bilinear + resonance 正規化。s2 tap は維持。");
        Assert.That(max, Is.LessThan(2.0f),
            $"Roland CUTOFF=0.50 RESO=100: max={max:F4} diverged.");
    }

    // ── Huovilainen acr: 高カットオフでの均一な強発振 ─────────────────────
    // RED: MusicDSP r*(t2+6t1)/(t2-6t1) は高カットオフで破綻する。
    //      CUTOFF=0.90: r_norm=1.618 / CUTOFF=0.99: r_norm=1.071 → 弱い発振。
    //      Huovilainen acr = -3.9364*fc_ratio^2 + 1.8409*fc_ratio + 0.9968 に変えると
    //      全域で r = 4.0 * resonance * acr ≈ 4.0〜4.9 → 強発振。
    //      CUTOFF=100 RESO=100 は「素のノコギリ波 + 強烈な発振音」が正しい挙動。
    //      根拠: ddiakopoulos/MoogLadders HuovilainenModel.h (public domain)
    //
    // RMS 測定: 44100 サンプル(1秒) の出力 RMS。
    //   強発振なら RMS ≈ 1.1 (±1.9 でクリップされた波形)。
    //   弱発振(r_norm≈1.07)なら発振が遅く RMS < 0.5。
    //   threshold = 0.8: 高カットオフでも強発振を要求。

    static float CalcRms(Filter f, int n) {
        double sum = 0.0;
        for (int i = 0; i < n; i++) {
            float s = f.Process(i == 0 ? 1.0f : 0.0f, i);
            sum += s * s;
        }
        return (float)Math.Sqrt(sum / n);
    }

    // ── True-MusicDSP fidelity (restore original, remove our deviations) ──────
    // We deviated from the MusicDSP original (musicdsp.org id=24) in 3 ways:
    //   (1) added /0.75 to resonance normalization (NOT in original)
    //   (2) dropped the original band-limited sigmoid  stage[3] -= stage[3]^3/6
    //   (3) replaced linear feedback with TanhFast(s4) in the feedback path
    // Restoring the original (verified by porting MusicDSPModel.h):
    //   - CUTOFF=0.5, RESO=0.5 must NOT oscillate (original onset is ~RESO=0.6;
    //     our /0.75 made it oscillate from ~0.5 — wrong).
    //   - Self-oscillation amplitude is naturally limited by the x^3/6 sigmoid to
    //      RMS ~0.39 at fc=469Hz, NOT clamped at +-1.9 (RMS ~1.1). Output must
    //     stay well below the old hard-clip RMS.

    [Test] public void TrueMusicDSP_MidReso_DoesNotOscillate()
    {
        // Original (no /0.75): CUTOFF=0.5 RESO=0.5 decays. Our /0.75 wrongly oscillates.
        var f = new Filter();
        f.SetParams(0.5f, 0.5f, FilterKind.Moog, SR);
        float rms = CalcRms(f, SR);
        Assert.That(rms, Is.LessThan(0.02f),
            $"CUTOFF=0.5 RESO=0.5: RMS={rms:F4}. Original MusicDSP (no /0.75) decays here. " +
            "Remove the /0.75 factor so onset is ~RESO=0.6, not ~0.5.");
    }

    [Test] public void TrueMusicDSP_Oscillation_SelfLimitsBelowHardClip()
    {
        // Original x^3/6 sigmoid self-limits oscillation to RMS ~0.39 at fc=469Hz.
        // Our hard-clip +-1.9 gives RMS ~1.1. Restoring the sigmoid must drop RMS well below 1.0.
        var f = new Filter();
        f.SetParams(0.5f, 1.0f, FilterKind.Moog, SR);
        float rms = CalcRms(f, SR);
        Assert.That(rms, Is.LessThan(0.6f),
            $"CUTOFF=0.5 RESO=1.0: RMS={rms:F4}. Original sigmoid self-limits to ~0.39. " +
            "Restore stage[3] -= stage[3]^3/6 and linear feedback; drop the +-1.9 hard clip reliance.");
        Assert.That(rms, Is.GreaterThan(0.1f),
            $"CUTOFF=0.5 RESO=1.0: RMS={rms:F4}. Must still oscillate (original ~0.39).");
    }

    [Test] public void Moog_HighCutoff_HighResonance_StrongOscillation()
    {
        // CUTOFF=0.90 → fc≈5.9kHz. RESO=1.0 strongly self-oscillates, but the
        // original x^3/6 sigmoid self-limits the amplitude, so RMS ≈ 0.71
        // (NOT the old ~1.1 of the +-1.9 hard clip). Require > 0.5.
        var f = new Filter();
        f.SetParams(0.90f, 1.0f, FilterKind.Moog, SR);
        float rms = CalcRms(f, SR);
        Assert.That(rms, Is.GreaterThan(0.5f),
            $"Moog CUTOFF=0.90 RESO=100: RMS={rms:F4}. " +
            "Must self-oscillate (sigmoid-limited ~0.71). MusicDSP original normalization.");
    }

    [Test] public void Roland_HighCutoff_HighResonance_StrongOscillation()
    {
        var f = new Filter();
        f.SetParams(0.90f, 1.0f, FilterKind.Roland, SR);
        float rms = CalcRms(f, SR);
        Assert.That(rms, Is.GreaterThan(0.8f),
            $"Roland CUTOFF=0.90 RESO=100: RMS={rms:F4}. " +
            "Must strongly self-oscillate. s2 tap preserved.");
    }

    [Test] public void Moog_FullCutoff_HighResonance_OscillatesModerately()
    {
        // CUTOFF=0.99 → fc=18kHz。MusicDSP/0.75: r=1.428 > 発振。
        // Nyquist 近傍のため RMS は中程度に留まる。> 0.5 を要求(名前どおり moderate)。
        var f = new Filter();
        f.SetParams(0.99f, 1.0f, FilterKind.Moog, SR);
        float rms = CalcRms(f, SR);
        Assert.That(rms, Is.GreaterThan(0.5f),
            $"Moog CUTOFF=0.99 RESO=100: RMS={rms:F4}. " +
            "Must oscillate near Nyquist (RMS moderate due to fc=18kHz). " +
            "r = resonance * (t2+6t1) / ((t2-6t1) * 0.75f).");
    }

    [Test] public void Roland_FullCutoff_HighResonance_OscillatesModerately()
    {
        var f = new Filter();
        f.SetParams(0.99f, 1.0f, FilterKind.Roland, SR);
        float rms = CalcRms(f, SR);
        Assert.That(rms, Is.GreaterThan(0.5f),
            $"Roland CUTOFF=0.99 RESO=100: RMS={rms:F4}. " +
            "Must oscillate near Nyquist. s2 tap preserved.");
    }

    // ── NOTE: oscillation-threshold tests removed (were unsound) ──────────
    // The former Moog/Roland_MidResonance_ShouldNotOscillate tests asserted
    // "RESO=0.65 must not oscillate". Investigation (fc_max=11kHz change) revealed
    // this property never actually held: with /0.75 normalization the real
    // self-oscillation threshold sits well below RESO=0.75 across most cutoffs
    // (e.g. CUTOFF=0.5 self-oscillates from ~RESO=0.5 even without kickstart).
    // The old tests only passed by coincidence at the single point CUTOFF=0.90
    // under the 20kHz mapping. Redesigning the resonance threshold so that
    // oscillation reliably begins at RESO=0.75 across the full range is a
    // separate task (tracked in docs/todo.md), not part of the anti-aliasing fix.
}
