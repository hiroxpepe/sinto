// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Audio;

namespace Signo.Tests.Scope;

/// <summary>
/// ScopeRenderer — pure rendering logic for the oscilloscope, isolated from WPF
/// so it can be unit-tested. A correct oscilloscope:
///   1. FIXED scale — shows true amplitude × gain, NEVER auto-normalises.
///   2. Zero-cross trigger — locks the waveform horizontally (stable display).
///   3. Maps the displayed sample window across the full canvas width.
///   4. Pitch-independent — same amplitude displays the same regardless of freq.
///   5. Steady signal → flat amplitude across the whole trace (no left/right skew).
/// Output is a list of (x, y) points in canvas coordinates.
/// </summary>
[TestFixture]
public class ScopeRendererTests
{
    const int SR = 44100;

    static float[] Sine(float freq, float amp, int count)
    {
        var b = new float[count];
        for (int i = 0; i < count; i++) b[i] = amp * MathF.Sin(2f * MathF.PI * freq * i / SR);
        return b;
    }

    static float PeakDeflection((double x, double y)[] pts, double cy, int fromIdx, int toIdx)
    {
        double peak = 0;
        for (int i = fromIdx; i < toIdx; i++) peak = Math.Max(peak, Math.Abs(pts[i].y - cy));
        return (float)peak;
    }

    // ── 1. Fixed scale: deflection linear in sample value ────────────
    [Test]
    public void Deflection_LinearInSampleValue()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0, TrigZero = false };
        double cy = 200;
        var quarter = r.Render(Sine(220f, 0.25f, 2000));
        var half    = r.Render(Sine(220f, 0.5f, 2000));
        double dq = PeakDeflection(quarter, cy, 0, quarter.Length);
        double dh = PeakDeflection(half, cy, 0, half.Length);
        Assert.That(dh / dq, Is.EqualTo(2.0).Within(0.05),
            "0.5 must deflect ~2x of 0.25 (fixed linear scale).");
    }

    // ── 2. No auto-normalisation: small signal stays small ───────────
    [Test]
    public void SmallSignal_StaysSmall_NotNormalised()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0, TrigZero = false };
        var small = r.Render(Sine(220f, 0.1f, 2000));   // amp 0.1
        var big   = r.Render(Sine(220f, 0.9f, 2000));   // amp 0.9
        double cy = 200;
        float pSmall = PeakDeflection(small, cy, 0, small.Length);
        float pBig   = PeakDeflection(big,   cy, 0, big.Length);
        Assert.That(pBig / pSmall, Is.EqualTo(9.0).Within(0.3),
            $"Amplitude ratio must be preserved (no normalisation). big/small={pBig / pSmall:F2}");
    }

    // ── 3. Points span the full canvas width ─────────────────────────
    [Test]
    public void Points_SpanFullWidth()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0, TrigZero = false };
        var pts = r.Render(Sine(220f, 0.8f, 2000));
        Assert.That(pts[0].x, Is.EqualTo(0).Within(1.0), "First point at x=0.");
        Assert.That(pts[^1].x, Is.EqualTo(800).Within(2.0), "Last point near x=Width.");
    }

    // ── 4. Pitch independence: same amp, different freq → same peak ──
    [Test]
    public void PitchIndependent_SameAmplitude()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 0.707, TrigZero = true };
        var low  = r.Render(Sine(110f, 0.8f, (int)(SR * 0.15)));
        var high = r.Render(Sine(880f, 0.8f, (int)(SR * 0.15)));
        double cy = 200;
        float pLow  = PeakDeflection(low,  cy, 0, low.Length);
        float pHigh = PeakDeflection(high, cy, 0, high.Length);
        Assert.That(pLow / pHigh, Is.EqualTo(1.0).Within(0.05),
            $"Same amplitude must display identically regardless of pitch. low/high={pLow / pHigh:F3}");
    }

    // ── 5. Steady sine → flat amplitude (THE bug) ────────────────────
    [Test]
    public void SteadySine_FlatAmplitude_NoLeftRightSkew()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 0.707, TrigZero = true };
        var pts = r.Render(Sine(333.6f, 0.8f, (int)(SR * 0.15)));
        double cy = 200;
        int q = pts.Length / 4;
        float pL = PeakDeflection(pts, cy, 0, q);
        float pR = PeakDeflection(pts, cy, pts.Length - q, pts.Length);
        Assert.That(pL / pR, Is.EqualTo(1.0).Within(0.05),
            $"Steady sine MUST be flat. left/right={pL / pR:F3}");
    }

    // ── 6. Zero-cross trigger: trace starts at upward crossing ───────
    [Test]
    public void ZeroCrossTrigger_StartsAtUpwardCrossing()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0, TrigZero = true };
        // Start mid-cycle so trigger must shift
        int n = 2000;
        var buf = new float[n];
        for (int i = 0; i < n; i++) buf[i] = MathF.Sin(2f * MathF.PI * 220f * i / SR + 1.5f);
        var pts = r.Render(buf);
        // First rendered sample should be near zero and rising → y near cy
        Assert.That(pts[0].y, Is.EqualTo(200).Within(8.0),
            "Triggered trace must start at a zero crossing (y≈center).");
    }

    // ── 7. DC offset removed (centred trace) ─────────────────────────
    [Test]
    public void DcOffset_Removed()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0, TrigZero = false };
        // Sine + large DC offset
        int n = 2000;
        var buf = new float[n];
        for (int i = 0; i < n; i++) buf[i] = 0.5f + 0.3f * MathF.Sin(2f * MathF.PI * 220f * i / SR);
        var pts = r.Render(buf);
        double cy = 200;
        // Mean Y should be at center (DC removed)
        double meanY = 0; foreach (var p in pts) meanY += p.y; meanY /= pts.Length;
        Assert.That(meanY, Is.EqualTo(cy).Within(3.0),
            "DC offset must be removed — trace centred on cy.");
    }

    // ── 8. Empty buffer → no points, no crash ────────────────────────
    [Test]
    public void EmptyBuffer_ReturnsEmpty()
    {
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0 };
        var pts = r.Render(Array.Empty<float>());
        Assert.That(pts.Length, Is.EqualTo(0));
    }
}

[TestFixture]
public class ScopeTriggerStabilityTests
{
    const int SR = 44100;

    // Generate a sine with a given starting phase offset (simulates frames
    // captured at different times — the ring buffer never starts on a cycle).
    static float[] SineWithPhase(float freq, float amp, int count, float phase)
    {
        var b = new float[count];
        for (int i = 0; i < count; i++)
            b[i] = amp * MathF.Sin(2f * MathF.PI * freq * i / SR + phase);
        return b;
    }

    // ── A steady tone must render IDENTICALLY regardless of capture phase ──
    // This is what "the waveform stands still" means: different frames (with
    // different starting phases) must produce the same on-screen points.
    [Test]
    public void SteadyTone_RendersIdentically_AcrossCapturePhases()
    {
        int displayWin = (int)(SR * 0.020); // 20ms display
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0, TrigZero = true,
            DisplaySamples = displayWin };
        // Capture more than the display window so the trigger has room to align.
        int captured = (int)(SR * 0.030); // 30ms captured
        float freq = 400f, amp = 0.8f;

        var frameA = r.Render(SineWithPhase(freq, amp, captured, 0.0f));
        var frameB = r.Render(SineWithPhase(freq, amp, captured, 2.7f));

        Assert.That(frameA.Length, Is.EqualTo(frameB.Length),
            "Triggered traces must have the SAME length every frame (no horizontal jitter).");

        double maxDiff = 0;
        for (int i = 0; i < frameA.Length; i++)
            maxDiff = Math.Max(maxDiff, Math.Abs(frameA[i].y - frameB[i].y));
        Assert.That(maxDiff, Is.LessThan(8.0),
            $"Triggered traces must overlap (stable display). maxDiff={maxDiff:F2}px");
    }

    // ── Trigger output length is constant across phases ──────────────
    [Test]
    public void TriggeredLength_IsConstant_RegardlessOfPhase()
    {
        int displayWin = (int)(SR * 0.020);
        var r = new ScopeRenderer { Width = 800, Height = 400, Gain = 1.0, TrigZero = true,
            DisplaySamples = displayWin };
        int captured = (int)(SR * 0.030);
        float freq = 400f, amp = 0.8f;

        int? len = null;
        foreach (float ph in new[] { 0f, 0.5f, 1.3f, 2.0f, 3.0f, 4.5f, 5.8f }) {
            var pts = r.Render(SineWithPhase(freq, amp, captured, ph));
            if (len == null) len = pts.Length;
            else Assert.That(pts.Length, Is.EqualTo(len.Value),
                $"Trace length must be identical across phases (phase={ph}).");
        }
    }

    // ── The displayed window must match TIME/DIV exactly ─────────────
    // If we ask for a 25ms display window, the trace must cover 25ms of the
    // signal — not "whatever is left after the trigger".
    [Test]
    public void DisplayWindow_CoversRequestedDuration()
    {
        // displayWindow = 25ms worth of samples
        int displayWin = (int)(SR * 0.025);
        var r = new ScopeRenderer {
            Width = 800, Height = 400, Gain = 1.0, TrigZero = true,
            DisplaySamples = displayWin
        };
        // Provide extra captured samples (window + 1 period headroom)
        int captured = displayWin + (int)(SR / 400f) + 10;
        var pts = r.Render(SineWithPhase(400f, 0.8f, captured, 1.1f));

        // 400Hz over 25ms = 10 cycles. Count upward zero crossings in the trace.
        double cy = 200;
        int crossings = 0;
        for (int i = 1; i < pts.Length; i++) {
            bool prevBelow = pts[i - 1].y > cy; // y>cy means sample<0
            bool curAbove  = pts[i].y <= cy;
            if (prevBelow && curAbove) crossings++;
        }
        Assert.That(crossings, Is.EqualTo(10).Within(1),
            $"25ms window of 400Hz must show ~10 cycles. Got {crossings}.");
    }
}

[TestFixture]
public class ScopeAutoGainTests
{
    const int SR = 44100;

    static float[] Sine(float freq, float amp, int count)
    {
        var b = new float[count];
        for (int i = 0; i < count; i++) b[i] = amp * MathF.Sin(2f * MathF.PI * freq * i / SR);
        return b;
    }

    static float PeakDefl((double x, double y)[] pts, double cy)
    {
        double pk = 0;
        foreach (var p in pts) pk = Math.Max(pk, Math.Abs(p.y - cy));
        return (float)pk;
    }

    // ── AutoGain makes a small signal fill the screen ────────────────
    // A tiny-amplitude steady sine (0.04, like the real sustain level) must,
    // after auto-gain settles, deflect close to the target fill height.
    [Test]
    public void AutoGain_SmallSignal_FillsScreen_AfterSettle()
    {
        var r = new ScopeRenderer {
            Width = 800, Height = 400, TrigZero = true,
            DisplaySamples = (int)(SR * 0.020), AutoGain = true
        };
        double cy = 200;
        int captured = (int)(SR * 0.030);

        // Run many frames so the smoothed gain converges.
        (double x, double y)[] pts = Array.Empty<(double, double)>();
        for (int f = 0; f < 120; f++) pts = r.Render(Sine(400f, 0.04f, captured));

        float peak = PeakDefl(pts, cy);
        // Target fill: ~65% of half-height = 0.65 * 200 = 130px. Allow a band.
        Assert.That(peak, Is.GreaterThan(110).And.LessThan(150),
            $"Auto-gain must make a small signal fill the screen. peak={peak:F1}px");
    }

    // ── AutoGain is STABLE: gain barely changes frame-to-frame ───────
    // This is the anti-jitter requirement: once settled, the peak deflection
    // must be essentially identical across consecutive frames.
    [Test]
    public void AutoGain_Stable_NoFrameToFrameJitter()
    {
        var r = new ScopeRenderer {
            Width = 800, Height = 400, TrigZero = true,
            DisplaySamples = (int)(SR * 0.020), AutoGain = true
        };
        double cy = 200;
        int captured = (int)(SR * 0.030);

        // Settle
        for (int f = 0; f < 120; f++) r.Render(Sine(400f, 0.04f, captured));

        // Now measure peak across several consecutive frames
        float prev = -1; float maxDelta = 0;
        for (int f = 0; f < 10; f++) {
            var pts = r.Render(Sine(400f, 0.04f, captured));
            float pk = PeakDefl(pts, cy);
            if (prev >= 0) maxDelta = Math.Max(maxDelta, Math.Abs(pk - prev));
            prev = pk;
        }
        Assert.That(maxDelta, Is.LessThan(2.0),
            $"Settled auto-gain must be stable (no jitter). maxDelta={maxDelta:F2}px");
    }

    // ── AutoGain pitch-independent: same amp different freq → same fill ─
    [Test]
    public void AutoGain_PitchIndependent()
    {
        double cy = 200;
        int captured = (int)(SR * 0.030);

        var rLow = new ScopeRenderer { Width=800, Height=400, TrigZero=true,
            DisplaySamples=(int)(SR*0.020), AutoGain=true };
        var rHigh = new ScopeRenderer { Width=800, Height=400, TrigZero=true,
            DisplaySamples=(int)(SR*0.020), AutoGain=true };

        (double x, double y)[] lo = Array.Empty<(double,double)>(), hi = Array.Empty<(double,double)>();
        for (int f=0; f<120; f++) { lo = rLow.Render(Sine(150f,0.3f,captured)); hi = rHigh.Render(Sine(900f,0.3f,captured)); }

        float pLo = PeakDefl(lo, cy), pHi = PeakDefl(hi, cy);
        Assert.That(pLo/pHi, Is.EqualTo(1.0).Within(0.1),
            $"Auto-gain fill must be pitch-independent. low/high={pLo/pHi:F3}");
    }

    // ── AutoGain off → fixed scale still works (regression) ──────────
    [Test]
    public void AutoGain_Off_UsesFixedGain()
    {
        var r = new ScopeRenderer {
            Width = 800, Height = 400, TrigZero = true, Gain = 1.0,
            DisplaySamples = (int)(SR * 0.020), AutoGain = false
        };
        double cy = 200;
        int captured = (int)(SR * 0.030);
        var pts = r.Render(Sine(400f, 0.5f, captured));
        float peak = PeakDefl(pts, cy);
        // Fixed: 0.5 * 0.65 * 200 * 1.0 = 65px
        Assert.That(peak, Is.EqualTo(65).Within(5),
            $"With AutoGain off, fixed scale must apply. peak={peak:F1}");
    }
}

[TestFixture]
public class ScopeReadabilityTests
{
    const int SR = 44100;
    static float[] Sine(float freq, float amp, int count)
    {
        var b = new float[count];
        for (int i = 0; i < count; i++) b[i] = amp * MathF.Sin(2f * MathF.PI * freq * i / SR);
        return b;
    }
    static float PeakDefl((double x, double y)[] pts, double cy)
    {
        double pk = 0; foreach (var p in pts) pk = Math.Max(pk, Math.Abs(p.y - cy));
        return (float)pk;
    }

    // ── Auto-gain must leave vertical headroom (not fill 100%) ───────
    // The waveform should fill comfortably (~65% of half-height), NOT slam
    // into the top and bottom edges.
    [Test]
    public void AutoGain_LeavesVerticalHeadroom()
    {
        var r = new ScopeRenderer {
            Width = 800, Height = 400, TrigZero = true,
            DisplaySamples = (int)(SR * 0.005 * 5), AutoGain = true
        };
        double cy = 200; // half-height = 200
        int captured = (int)(SR * 0.040);
        (double x, double y)[] pts = Array.Empty<(double, double)>();
        for (int f = 0; f < 150; f++) pts = r.Render(Sine(213f, 0.04f, captured));

        float peak = PeakDefl(pts, cy);
        // Must NOT exceed ~75% of half-height (leaves headroom), but still be visible.
        Assert.That(peak, Is.GreaterThan(110).And.LessThan(155),
            $"Auto-gain must fill comfortably with headroom (~65%). peak={peak:F1}/200");
    }

    // ── Default time window shows a readable number of cycles ────────
    // At the DEFAULT TIME/DIV, an A4 (440Hz) tone must show roughly 4-8
    // cycles across the screen — not 30 (too cramped) nor 2 (too sparse).
    [Test]
    public void DefaultTimeWindow_ShowsReadableCycleCount()
    {
        double defaultTimeDivMs = OscilloscopeDefaults.DefaultTimeDivMs;
        int divs = OscilloscopeDefaults.GridDivs;
        double windowMs = defaultTimeDivMs * divs;
        double cycles = 440.0 * windowMs / 1000.0;
        Assert.That(cycles, Is.GreaterThan(3.0).And.LessThan(5.0),
            $"Default window must show ~4 cycles for A4. Got {cycles:F1} ({windowMs:F0}ms).");
    }
}
