// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Sinto.Core.Preset;

/// <summary>Load .sinto JSON preset files. Returns Default on any error.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public static class Loader {
#nullable enable
    private static readonly JsonSerializerOptions OPTIONS = new() {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
    };

    public static Preset Load(string file_path) {
        try {
            if (string.IsNullOrEmpty(file_path) || !File.Exists(file_path))
                return Preset.Default;
            byte[] data = File.ReadAllBytes(file_path);
            return LoadFromBytes(data);
        } catch {
            return Preset.Default;
        }
    }

    public static Preset LoadFromBytes(ReadOnlySpan<byte> data) {
        try {
            if (data.IsEmpty) return Preset.Default;
            var raw = JsonSerializer.Deserialize<Preset>(data, OPTIONS);
            if (raw == null) return Preset.Default;
            return Validator.Validate(raw);
        } catch {
            return Preset.Default;
        }
    }

    private static byte[] Deobfuscate(byte[] data) {
        const byte KEY_BASE = 0x5A;
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++) {
            result[i] = (byte)(data[i] ^ (KEY_BASE + (i & 0x0F)));
        }
        return result;
    }
}
