// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Preset;

/// <summary>
/// Full synthesizer preset. Loaded from .sinto JSON.
/// Used with double-buffered Interlocked.Exchange for hot-swap.
/// All parameters validated on load via Validator.
/// </summary>
public sealed class Preset
{
    public string          name           { get; init; } = string.Empty;
    public string          version        { get; init; } = "1.0";
    public OscPreset osc1          { get; init; } = OscPreset.Default;
    public OscPreset osc2          { get; init; } = OscPreset.Default;
    public FilterPreset     filter        { get; init; } = FilterPreset.Default;
    public EnvPreset   amp_envelope   { get; init; } = EnvPreset.Default;
    public EnvPreset   filter_envelope{ get; init; } = EnvPreset.Default;
    public EnvPreset   pitch_envelope { get; init; } = EnvPreset.Default;
    public LfoPreset        lfo1          { get; init; } = LfoPreset.Default;
    public LfoPreset        lfo2          { get; init; } = LfoPreset.Default;
    public float            portamento_time{ get; init; } = 0f;
    public FxPreset    effects       { get; init; } = FxPreset.Default;
    public Sinto.Core.Synth.RetroMode retro_mode { get; init; } = Sinto.Core.Synth.RetroMode.Clean;

    public static readonly Preset Default = new() { name = "Default" };
}
