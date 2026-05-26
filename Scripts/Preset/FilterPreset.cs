#nullable enable
using Sinto.Core.Filter;

namespace Sinto.Core.Preset;

public sealed class FilterPreset
{
    public FilterMode Mode      { get; init; } = FilterMode.Roland;
    public float      Cutoff    { get; init; } = 1.0f;   // [0.001, 0.999]
    public float      Resonance { get; init; } = 0.0f;   // [0.0, 1.0]
    public float      EnvAmt    { get; init; } = 0.0f;   // [-1.0, +1.0]
    public float      KeyFollow { get; init; } = 0.0f;   // [0.0, 1.0]

    public static readonly FilterPreset Default = new();
}
