#nullable enable
using Sinto.Core.Synth;

namespace Sinto.Core.Preset;

public sealed class EffectsPreset
{
    // Chorus
    public int   ChorusMode  { get; init; } = 1;     // 1 or 2
    public float ChorusRate  { get; init; } = 0.5f;
    public float ChorusDepth { get; init; } = 0.3f;
    public float ChorusMix   { get; init; } = 0.0f;

    // Reverb
    public float ReverbRoomSize { get; init; } = 0.5f;
    public float ReverbDamping  { get; init; } = 0.5f;
    public float ReverbMix      { get; init; } = 0.0f;

    // Delay
    public float DelayTime     { get; init; } = 0.25f;  // seconds
    public float DelayFeedback { get; init; } = 0.3f;   // [0.0, 0.95]
    public float DelayMix      { get; init; } = 0.0f;
    public bool  DelayTempoSync{ get; init; } = false;

    // Retro
    public RetroMode RetroMode { get; init; } = RetroMode.Clean;

    public static readonly EffectsPreset Default = new();
}
