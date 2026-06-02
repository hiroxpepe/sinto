// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Signo.Core.Signal;

namespace Signo.Core.Effects;

/// <summary>Common base for all audio effects. Extends ISignal.</summary>
public interface IEffect : ISignal { }

/// <summary>
/// Send-return effect: Process outputs full-wet signal.
/// SendBus applies the send level externally before and after calling Process.
/// </summary>
public interface ISendEffect : IEffect { }

/// <summary>
/// Insert effect: Process adds wet to the existing dry signal in-place.
/// buffer[i] = dry[i] + wet[i] * Send.
/// </summary>
public interface IInsertEffect : IEffect
{
    float Send { get; set; }
}
