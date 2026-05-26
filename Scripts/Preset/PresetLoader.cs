// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Preset;

/// <summary>
/// Load .sinto JSON preset files.
/// Speed-bump obfuscation applied if file is protected (XOR, NOT cryptographic).
/// Returns Default preset on any error — NEVER throws to caller.
/// </summary>
public static class PresetLoader
{
    /// <summary>Load from filesystem path. Validates on load.</summary>
    public static SintoPreset Load(string filePath)
        => throw new NotImplementedException();

    /// <summary>Load from byte array (memory-only, no filesystem write).</summary>
    public static SintoPreset LoadFromBytes(ReadOnlySpan<byte> data)
        => throw new NotImplementedException();

    /// <summary>XOR obfuscation — speed bump only, not cryptographic security.</summary>
    private static byte[] Deobfuscate(byte[] data)
        => throw new NotImplementedException();
}
