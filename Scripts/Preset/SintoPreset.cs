// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Preset;

/// <summary>
/// Full synthesizer preset. Loaded from .sinto JSON.
/// Used with double-buffered Interlocked.Exchange for hot-swap.
/// All parameters validated on load via PresetValidator.
/// </summary>
public sealed class SintoPreset
{
    public string          Name           { get; init; } = string.Empty;
    public string          Version        { get; init; } = "1.0";
    public OscillatorPreset Osc1          { get; init; } = OscillatorPreset.Default;
    public OscillatorPreset Osc2          { get; init; } = OscillatorPreset.Default;
    public FilterPreset     Filter        { get; init; } = FilterPreset.Default;
    public EnvelopePreset   AmpEnvelope   { get; init; } = EnvelopePreset.Default;
    public EnvelopePreset   FilterEnvelope{ get; init; } = EnvelopePreset.Default;
    public EnvelopePreset   PitchEnvelope { get; init; } = EnvelopePreset.Default;
    public LFOPreset        Lfo1          { get; init; } = LFOPreset.Default;
    public LFOPreset        Lfo2          { get; init; } = LFOPreset.Default;
    public float            PortamentoTime{ get; init; } = 0f;
    public EffectsPreset    Effects       { get; init; } = EffectsPreset.Default;
    public Sinto.Core.Synth.RetroMode RetroMode { get; init; } = Sinto.Core.Synth.RetroMode.Clean;

    public static readonly SintoPreset Default = new() { Name = "Default" };
}
