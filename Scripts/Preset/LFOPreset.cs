#nullable enable
using Sinto.Core.Synth;

namespace Sinto.Core.Preset;

public sealed class LFOPreset
{
    public LFOWave        Wave         { get; init; } = LFOWave.Sine;
    public float          RateOrSync   { get; init; } = 1.0f;  // Hz or note value
    public float          Depth        { get; init; } = 0.0f;  // [0.0, 1.0]
    public bool           TempoSync    { get; init; } = false;
    public LFODestination Destinations { get; init; } = LFODestination.None;

    public static readonly LFOPreset Default = new();
}
