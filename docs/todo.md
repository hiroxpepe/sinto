# Sinto TODO List

## Hot path optimization (pick up during WPF phase)
- [ ] TanhFast: remove division → polynomial approximation (Calc.cs only)
- [ ] PitchRatioFast: MathF.Exp → bit-hack pow2 approximation (Calc.cs only)
- [ ] RingBuffer: add StructLayout(Explicit) + FieldOffset for false-sharing safety

## Architecture sprint (dedicated)
- [ ] Control-rate modulation: LFO/Env updated every 16-32 samples (not per-sample)

## New feature sprint (dedicated)
- [ ] WaveType.PCM — LinnDrum-style 1-shot sampler
  - Pre-conditions to design first:
    - Voice release on playback end (Envelope coordination)
    - SampleBank ownership and lifecycle (WPF vs Unity)
    - Mono-only or stereo support decision
    - Asset licensing policy (no borrowed samples in commercial build)
    - Sequencer strategy (PCM drums alone ≠ rhythm)
    - Target: low bitrate / low samplerate baked assets (retro aesthetic)


## Resonance oscillation threshold redesign (separate task)
The `/0.75` normalization was intended to place self-oscillation onset at
user RESO=0.75 uniformly. Investigation during the fc_max=11kHz anti-aliasing
fix revealed this does not hold: actual onset sits well below 0.75 at most
cutoffs (CUTOFF=0.5 self-oscillates from ~RESO=0.5 even without kickstart).
Need to redesign so oscillation reliably begins at RESO=0.75 across full range.
Removed the unsound Moog/Roland_MidResonance_ShouldNotOscillate tests that
only passed by coincidence at CUTOFF=0.90 under the old 20kHz mapping.
