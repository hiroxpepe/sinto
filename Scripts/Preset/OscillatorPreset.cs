#nullable enable
using Sinto.Core.Synth;

namespace Sinto.Core.Preset;

public sealed class OscillatorPreset
{
    public WaveType   Wave        { get; init; } = WaveType.Sine;
    public InterpMode Interp      { get; init; } = InterpMode.Linear;
    public float      DetuneCents { get; init; } = 0f;    // [-100, +100]
    public float      PulseWidth  { get; init; } = 0.5f;  // [0.01, 0.99]
    public float      Level       { get; init; } = 1.0f;  // [0.0, 1.0]

    public static readonly OscillatorPreset Default = new();
}
