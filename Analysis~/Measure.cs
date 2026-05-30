// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
//
// Filter analysis harness — runs the REAL Filter (linked from Scripts/Synth)
// and writes its measured output as CSV for visualize.py to plot.
//
// Nothing here models the filter; it only drives the production struct and
// records what it actually produces. If Filter.cs changes, rebuild and the
// numbers change with it.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Signo.Core.Synth;

internal static class Measure {
    const int SR = 44100;

    static void Main(string[] args) {
        string outDir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(outDir);

        // 1) Per-CUTOFF impulse responses (raw waveforms) at RESO=100.
        //    visualize.py reads these for time-domain and FFT analysis.
        WriteImpulseResponses(Path.Combine(outDir, "impulse_moog.csv"),
            FilterKind.Moog, resonance: 1.0f);
        WriteImpulseResponses(Path.Combine(outDir, "impulse_roland.csv"),
            FilterKind.Roland, resonance: 1.0f);

        // 2) Full-range CUTOFF 0..100 sweeps at RESO=100, with the E1 (41.2Hz)
        //    sawtooth note so the recording is the basis+oscillation MIX the ear
        //    hears. Five sweep SPEEDS, to see how the step depends on how fast the
        //    cutoff knob is moved (fast "byao!" vs slow pad-like). The cutoff
        //    smoother in the live engine means fast sweeps may behave differently.
        const float E1 = 41.2f;
        float[] speeds = { 0.3f, 0.7f, 1.5f, 3.0f, 6.0f }; // seconds for 0->100
        foreach (var sec in speeds) {
            string tag = sec.ToString("0.0", CultureInfo.InvariantCulture).Replace(".", "p");
            WriteSweep(Path.Combine(outDir, $"sweep_moog_{tag}.csv"),
                FilterKind.Moog, 1.0f, 0.0f, 1.0f, sec, sawHz: E1);
            WriteSweep(Path.Combine(outDir, $"sweep_roland_{tag}.csv"),
                FilterKind.Roland, 1.0f, 0.0f, 1.0f, sec, sawHz: E1);
        }

        Console.WriteLine("done");
    }

    // Render a 1-second impulse response for each CUTOFF in [start..end].
    // CSV layout: first column = CUTOFF (x10, int e.g. 955 = 95.5), rest = samples.
    static void WriteImpulseResponses(string path, FilterKind kind, float resonance) {
        using var w = new StreamWriter(path);
        int n = SR; // 1 second
        // 0.5 steps across 0..100 (201 points) for precise step localisation.
        for (int half = 0; half <= 200; half++) {
            float cutoff01 = half * 0.5f / 100f;   // 0.000 .. 1.000
            int label = half * 5;                  // 0 .. 1000  (==CUTOFF*10)
            var f = new Filter();
            f.SetParams(cutoff01, resonance, kind, SR);
            var sb = new StringBuilder();
            sb.Append(label);
            for (int i = 0; i < n; i++) {
                float s = f.Process(i == 0 ? 1.0f : 0.0f, i);
                sb.Append(',');
                sb.Append(s.ToString("R", CultureInfo.InvariantCulture));
            }
            w.WriteLine(sb.ToString());
        }
    }

    // Full-range cutoff sweep through the SAME 60Hz cutoff smoother the live
    // engine uses (Voices.cs SmoothedCutoff). The raw knob position is the linear
    // ramp; the smoother is what the filter actually receives. This is why sweep
    // SPEED matters: a fast "byao!" outruns the smoother differently than a slow
    // pad sweep. sawHz>0 adds the played E1 note so we record the basis+osc MIX.
    // CSV layout: one value per line = output sample. Header line = metadata.
    static void WriteSweep(string path, FilterKind kind, float resonance,
        float cutoffStart, float cutoffEnd, float seconds, float sawHz) {
        using var w = new StreamWriter(path);
        int n = (int)(SR * seconds);
        w.WriteLine($"# sr={SR} n={n} start={cutoffStart} end={cutoffEnd} reso={resonance} kind={kind} sawHz={sawHz} smoothHz=60");
        var f = new Filter();
        // 60Hz cutoff smoother, exactly as Voices.cs initialises SmoothedCutoff.
        var cutoffSmoother = new Smoother(cutoffStart, 60f, SR);
        // resonance also smoothed (20Hz) as in the engine, though it is constant here.
        f.SetParams(cutoffStart, resonance, kind, SR);
        double phase = 0.0;
        double phaseInc = sawHz / SR;
        for (int i = 0; i < n; i++) {
            float t = (float)i / (n - 1);
            float rawCutoff = cutoffStart + (cutoffEnd - cutoffStart) * t; // knob position
            cutoffSmoother.SetTarget(rawCutoff);
            float smoothed = cutoffSmoother.Tick();                        // what filter gets
            f.SetParams(smoothed, resonance, kind, SR);
            float input;
            if (sawHz > 0f) {
                input = (float)(2.0 * phase - 1.0) * 0.5f;
                phase += phaseInc;
                if (phase >= 1.0) phase -= 1.0;
            } else {
                input = i == 0 ? 1.0f : 0.0f;
            }
            float s = f.Process(input, i);
            w.WriteLine(s.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
