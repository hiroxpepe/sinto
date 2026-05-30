# Filter Analysis (Analysis~)

Measures the **real** `Scripts/Synth/Filter.cs` and shows, as **line charts**,
how a full **CUTOFF 0..100 sweep** behaves at **five sweep speeds** (fast
"byao!" 0.3s .. slow 6s). RESO=100, driven through the real **60Hz cutoff
smoother** (Voices.cs path), with an **E1 (41.2Hz) sawtooth** note + the
self-oscillation = the MIX the ear actually hears.

This is the FIXED analysis used to compare filter revisions on one ruler.
The `~` suffix keeps the folder out of Unity (like `Audition~` / `Tests~`).

## Why this is trustworthy

- `Analysis.csproj` **links the production sources** (`Filter.cs`, `Calc.cs`,
  `Smoother.cs`, `Denormal.cs`) instead of copying them, so the charts always
  reflect the shipped DSP and can never silently drift.
- Frequency is measured by **instantaneous local zero-crossings**, NOT windowed
  FFT. Windowed FFT smears a fast sweep and exaggerates the lag. The zero-cross
  method is validated to **0.0% error on a known accelerating chirp** (printed
  as a self-test on every run).

## Requirements

- .NET 8 SDK (same one used for the test suite)
- Node.js (any recent version; **zero npm packages**). No Python.

## Run

Windows (PowerShell):

    powershell -ExecutionPolicy Bypass -File "Analysis~\run_analysis.ps1"

Linux / macOS / WSL / Git Bash:

    ./run_analysis.sh

This builds and runs `Measure.cs` (the real Filter through the 60Hz smoother),
writing CSVs to `data/`, then renders `data/sweep_speed_moog.svg` and
`data/sweep_speed_roland.svg`. Open the `.svg` in a browser or VS Code.

Manual steps:

    dotnet run --project Analysis.csproj -c Release -- data
    node visualize.js data moog
    node visualize.js data roland

## The three line charts (fixed order, five speed curves each)

1. **Pitch (semitone)** vs CUTOFF
2. **Loudness (dB)** vs CUTOFF
3. **Timbre (oscillation Hz)** vs CUTOFF

Each curve is one sweep speed. Comparing speeds shows where (and whether) a fast
sweep fails to track what a slow sweep reaches.

## Comparing revisions

`data/baseline_*.svg` holds the charts of the filter BEFORE a revision. After
changing `Filter.cs`, re-run and compare the new `sweep_speed_*.svg` against the
saved `baseline_*.svg` on the identical measurement.

## Honest limits

Physical signal measurements, not a model of hearing. They show whether the
signal itself moves smoothly; the final judgement of how it sounds is the
listener's. The very low CUTOFF region is limited by short-time resolution;
judge from the mid/high range.

## Files

- `Analysis.csproj` - links the real Filter sources + the harness
- `Measure.cs`      - runs the real Filter through the 60Hz smoother, writes CSVs
- `visualize.js`    - zero-dependency Node: self-test + zero-cross tracking + SVG line charts
- `run_analysis.ps1` / `run_analysis.sh` - one-command build + visualize
