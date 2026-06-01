// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Signo.Core.Audio;

/// <summary>
/// Shared oscilloscope display defaults, kept in Signo.Core so both the WPF
/// window and the unit tests reference the same values.
/// </summary>
public static class OscilloscopeDefaults
{
    /// <summary>Horizontal grid divisions across the canvas.</summary>
    public const int GridDivs = 5;

    /// <summary>
    /// Default TIME/DIV in milliseconds = the slider's CENTRE position, so
    /// the user can adjust both shorter and longer from the default view.
    /// A4 (440Hz) shows ~4 cycles (1.8ms × 5 = 9ms). Range is 0.6-5.4ms.
    /// </summary>
    public const double DefaultTimeDivMs = 1.8;

    /// <summary>
    /// Vertical fill factor for auto-gain: the settled trace peaks at this
    /// fraction of half-height, leaving headroom so it never slams the edges.
    /// </summary>
    public const double AutoGainFill = 0.65;
}
