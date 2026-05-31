// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Effects;

/// <summary>Common base for all audio effects.</summary>
public interface IAudioEffect
{
    void Process(Span<float> buffer, int channels);
    void Reset();
    bool enabled { get; set; }
}

/// <summary>
/// Send-return effect: Process outputs full-wet signal.
/// SendBus applies the send level externally before and after calling Process.
/// </summary>
public interface ISendEffect : IAudioEffect { }

/// <summary>
/// Insert effect: Process adds wet to the existing dry signal in-place.
/// buffer[i] = dry[i] + wet[i] * Send.
/// </summary>
public interface IInsertEffect : IAudioEffect
{
    float Send { get; set; }
}
