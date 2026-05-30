# Gemini Review Analysis
## Source: Gemini conversation (2025-05)
## Purpose: Reference for future improvements — NOT immediate action items

---

## Verdict on usefulness

### ACTIONABLE (concrete, technically sound)

1. **Control-rate modulation** (highest priority)
   - Current: LFO x2, Envelope x3 updated every sample (44100Hz)
   - Fix: Update modulation at control-rate (every 16-32 samples, ~1ms)
   - Impact: 16x reduction in LFO/Env CPU cost
   - Risk: Minimal — human ear can't detect 1ms modulation lag

2. **Hot path math — PitchRatioFast**
   - Current: `MathF.Exp(semitones * LN2_DIV_12)` called every sample
   - Fix: Bit-hack pow2 approximation (e.g. Schraudolph's method)
   - Impact: Significant on ARM where exp() is expensive

3. **Hot path math — TanhFast division**
   - Current: Contains division `x*(27+x2)/(27+9*x2)`
   - Fix: Polynomial approximation without division, or LUT
   - Impact: ARM division latency is 4-10x vs multiplication

4. **RingBuffer StructLayout guarantee**
   - Current: `class` with padding fields `_p1.._p14` for false sharing prevention
   - Risk: IL2CPP may reorder fields — false sharing could silently return
   - Fix: `[StructLayout(LayoutKind.Explicit)]` + `[FieldOffset]` for header/tail

### NOTED (valid but lower priority or already known)

5. **Branch prediction in Envelope/LFO switch statements**
   - Valid concern for hot path
   - Mitigation: block processing naturally amortizes this

6. **SIMD / Burst Compiler**
   - Valid for Unity target
   - Out of scope for current pure C# architecture — revisit if Unity port planned

### DISMISSIBLE (praise / no actionable content)

- All the "beautiful code" and "best practices" commentary
- "素質は商用トップクラスの原石" — correct but not actionable
- Repetitive summary paragraphs

---

## Priority order for future implementation

| Priority | Item | Estimated impact |
|----------|------|-----------------|
| 1 | Control-rate modulation (LFO/Env block processing) | CPU -50~70% |
| 2 | PitchRatioFast → bit-hack pow2 | CPU -moderate (ARM) |
| 3 | TanhFast → division-free | CPU -small |
| 4 | RingBuffer StructLayout fix | Stability (edge case) |

---

## WPF front-end development phase — pickup list
> Incorporate these incrementally as opportunities arise during WPF work.
> All changes follow TDD red→green cycle as usual.

| # | Item | When to pick up |
|---|------|----------------|
| 1 | TanhFast → division-free polynomial | Any time — isolated change in Calc.cs |
| 2 | PitchRatioFast → bit-hack pow2 | Any time — isolated change in Calc.cs |
| 3 | RingBuffer StructLayout fix | When touching threading/audio pipeline |
| 4 | Control-rate modulation (LFO/Env) | Dedicated sprint — architecture change |
| 5 | WaveType.PCM — LinnDrum-style sampler | Dedicated sprint — Oscillator extension |

---

## Notes

- Gemini's "Burst Compiler / SIMD" suggestion assumes Unity target.
  Current architecture is pure C# — keep as future option only.
- Control-rate implementation requires TDD approach:
  verify modulation continuity (no audible stepping at block boundaries).
- All changes must go through red→green TDD cycle as usual.

