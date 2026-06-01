// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Audio;

/// <summary>
/// Pure oscilloscope rendering logic, independent of any UI framework so it can
/// be unit-tested and reused. Converts a mono sample window into canvas points.
///
/// Design (a correct oscilloscope):
///   - FIXED scale: displays true amplitude x Gain. NEVER auto-normalises to
///     the buffer peak (that makes decaying waves look skewed, low notes bigger).
///   - Zero-cross trigger with FIXED-LENGTH window: the trace ALWAYS starts at
///     an upward zero crossing AND always renders exactly DisplaySamples samples.
///     This is what makes a steady tone stand perfectly still: every frame is
///     aligned to the same phase AND has the same length, so there is zero
///     horizontal jitter. (Previous bug: trace length was "whatever was left
///     after the trigger", so it stretched/shrank every frame = jitter.)
///   - Full-width mapping: the fixed-length window is stretched across Width.
///   - DC removal: centres the trace vertically.
///
/// To trigger correctly the caller must supply MORE samples than DisplaySamples
/// (at least one extra period of headroom) so the renderer can slide forward to
/// the first zero crossing and still have DisplaySamples samples remaining.
/// </summary>
public sealed class ScopeRenderer
{
    /// <summary>Canvas width in pixels.</summary>
    public double Width { get; set; } = 800;

    /// <summary>Canvas height in pixels.</summary>
    public double Height { get; set; } = 400;

    /// <summary>Fixed display gain (vertical scale). Applied directly to samples.</summary>
    public double Gain { get; set; } = 0.707;

    /// <summary>Vertical fill factor: fraction of half-height a full-scale sample uses.</summary>
    public double Fill { get; set; } = 0.65;

    /// <summary>When true, the trace starts at the first upward zero crossing.</summary>
    public bool TrigZero { get; set; } = true;

    /// <summary>
    /// Number of samples to display (the time window). When 0, the whole input
    /// (minus the trigger offset) is used. When set, the renderer emits exactly
    /// this many points after the trigger, guaranteeing a jitter-free trace.
    /// </summary>
    public int DisplaySamples { get; set; } = 0;

    /// <summary>
    /// When true, the vertical scale auto-adjusts so the waveform fills the
    /// screen — but via a SMOOTHED (exponentially-followed) gain, not the raw
    /// per-frame peak. Raw-peak normalisation jitters; a smoothed follower
    /// converges to a steady value for a steady tone, so it stays still while
    /// still filling the screen. Gain (the manual setting) is ignored when on.
    /// </summary>
    public bool AutoGain { get; set; } = false;

    // Smoothed auto-gain state (persists across Render calls / frames).
    double _autoGain = 1.0;
    bool   _autoGainInit;

    /// <summary>
    /// Render a mono sample window to canvas points. The input is not modified.
    /// Returns points ordered left to right across the full Width.
    /// </summary>
    public (double x, double y)[] Render(float[] samples)
    {
        if (samples == null || samples.Length == 0) return Array.Empty<(double, double)>();

        double cy = Height / 2.0;

        // DC removal (centres the trace). NOT amplitude normalisation; only
        // removes a constant offset so the wave sits on the centre line.
        double mean = 0;
        for (int i = 0; i < samples.Length; i++) mean += samples[i];
        mean /= samples.Length;

        // Output length: fixed window if requested, else everything (minus a
        // small trigger headroom so the zero-cross search has room to slide).
        int outLen;
        if (DisplaySamples > 0) {
            outLen = Math.Min(DisplaySamples, samples.Length);
        } else if (TrigZero && samples.Length > 4) {
            // Reserve up to ~1/4 of the buffer as headroom for the trigger so a
            // steady tone still locks even when no explicit window is set.
            outLen = samples.Length - Math.Min(samples.Length / 4, samples.Length - 1);
        } else {
            outLen = samples.Length;
        }

        // Zero-cross trigger: first upward crossing within the headroom region
        // so that, after triggering, outLen samples still remain.
        int start = 0;
        if (TrigZero) {
            int searchEnd = Math.Max(1, samples.Length - outLen);
            for (int i = 1; i <= searchEnd; i++) {
                double prev = samples[i - 1] - mean;
                double cur  = samples[i] - mean;
                if (prev < 0 && cur >= 0) { start = i; break; }
            }
        }

        // Clamp so we never read past the end.
        if (start + outLen > samples.Length) start = samples.Length - outLen;
        if (start < 0) start = 0;

        // ── Determine vertical scale ────────────────────────────────────
        // Fixed mode: vScale = cy * Fill * Gain (true amplitude × manual gain).
        // Auto mode: measure the window peak, then SMOOTHLY follow it so the
        // wave fills ~Fill of the screen. Smoothing (not raw peak) is what keeps
        // a steady tone from jittering: the gain converges and then holds.
        double vScale;
        if (AutoGain) {
            double peak = 0;
            for (int i = 0; i < outLen; i++) {
                double a = Math.Abs(samples[start + i] - mean);
                if (a > peak) peak = a;
            }
            // Target gain that would make this peak reach Fill of half-height.
            double target = peak > 1e-6 ? Fill / peak : _autoGain;
            // Clamp target to a sane range so silence doesn't blow up the gain.
            if (target > 200.0) target = 200.0;
            if (target < 0.05) target = 0.05;
            if (!_autoGainInit) { _autoGain = target; _autoGainInit = true; }
            else {
                // Exponential follower (~0.08 per frame ≈ a few hundred ms to settle).
                const double k = 0.08;
                _autoGain += (target - _autoGain) * k;
            }
            vScale = cy * _autoGain;
        } else {
            vScale = cy * Fill * Gain;
        }

        // ── Map across full width with the chosen vertical scale ────────
        var pts = new (double x, double y)[outLen];
        double xStep = outLen > 1 ? Width / (outLen - 1) : 0;
        for (int i = 0; i < outLen; i++) {
            double s = samples[start + i] - mean;
            double x = i * xStep;
            double y = cy - s * vScale;
            pts[i] = (x, y);
        }
        return pts;
    }
}
