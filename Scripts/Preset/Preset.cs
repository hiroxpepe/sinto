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
    public string          Name           { get; init; } = string.Empty;
    public string          Version        { get; init; } = "1.0";
    public OscPreset Osc1          { get; init; } = OscPreset.Default;
    public OscPreset Osc2          { get; init; } = OscPreset.Default;
    public FilterPreset     Filter        { get; init; } = FilterPreset.Default;
    public EnvPreset   AmpEnvelope   { get; init; } = EnvPreset.Default;
    public EnvPreset   FilterEnvelope{ get; init; } = EnvPreset.Default;
    public EnvPreset   PitchEnvelope { get; init; } = EnvPreset.Default;
    public LfoPreset        Lfo1          { get; init; } = LfoPreset.Default;
    public LfoPreset        Lfo2          { get; init; } = LfoPreset.Default;
    public float            PortamentoTime{ get; init; } = 0f;
    public FxPreset    Effects       { get; init; } = FxPreset.Default;
    public Sinto.Core.Synth.RetroMode RetroMode { get; init; } = Sinto.Core.Synth.RetroMode.Clean;

    public static readonly Preset Default = new() { Name = "Default" };
}
