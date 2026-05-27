#nullable enable
using Sinto.Core.Synth;

namespace Sinto.Core.Preset;

public sealed class LfoPreset
{
    public LfoWave        Wave         { get; init; } = LfoWave.Sine;
    public float          RateOrSync   { get; init; } = 1.0f;  // Hz or note value
    public float          Depth        { get; init; } = 0.0f;  // [0.0, 1.0]
    public bool           TempoSync    { get; init; } = false;
    public LfoTarget Destinations { get; init; } = LfoTarget.None;

    public static readonly LfoPreset Default = new();
}
