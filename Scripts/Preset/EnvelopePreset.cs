#nullable enable

namespace Sinto.Core.Preset;

public sealed class EnvelopePreset
{
    public float Attack  { get; init; } = 0.01f;  // [0.001, 10.0]
    public float Decay   { get; init; } = 0.1f;   // [0.001, 10.0]
    public float Sustain { get; init; } = 0.8f;   // [0.0, 1.0]
    public float Release { get; init; } = 0.2f;   // [0.001, 20.0]

    public static readonly EnvelopePreset Default     = new();
    public static readonly EnvelopePreset Percussive  = new() { Attack=0.001f, Decay=0.1f,  Sustain=0f,   Release=0.1f };
    public static readonly EnvelopePreset Pad         = new() { Attack=0.5f,   Decay=0.3f,  Sustain=0.9f, Release=1.0f };
}
