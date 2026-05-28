// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Sinto.Core.Audio;

/// <summary>Audio-thread event kinds exchanged through the engine queue.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public enum EventKind : byte {
    None         = 0,  // MUST be 0 — default(Event).Kind is None, not NoteOn
    NoteOn       = 1,
    NoteOff      = 2,
    Pause        = 3,
    Resume       = 4,
    SetVoiceLimit = 5,
    SwapPreset   = 6,
    SetBPM       = 7,
    SustainPedal = 8,  // CC64: FloatParam=1.0 down, 0.0 up
}

// PACKING NOTE:
// [StructLayout(LayoutKind.Sequential, Pack = 1)] would give exact 19-byte size,
// but Pack=1 can cause misaligned memory access on ARM (Unity IL2CPP warning).
// LayoutKind.Sequential (default pack) adds ~1 byte padding → ~20 bytes.
// This is acceptable: 20 bytes fits easily in a cache line (64 bytes).
// Do NOT use Pack=1 unless profiling proves it necessary on target hardware.
/// <summary>Immutable event payload for engine/audio thread communication.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Event : System.IEquatable<Event> {
    public readonly EventKind Kind;
    public readonly ushort OffsetFrames;
    public readonly int    IntParam;
    public readonly float  FloatParam;
    public readonly int    TrackId;
    public readonly int    Priority;

    public Event(EventKind kind, ushort offsetFrames = 0,
        int intParam = 0, float floatParam = 0f, int trackId = 0, int priority = 0) {
        Kind = kind; OffsetFrames = offsetFrames;
        IntParam = intParam; FloatParam = floatParam;
        TrackId = trackId; Priority = priority;
    }
    public bool Equals(Event o)
        => Kind == o.Kind && OffsetFrames == o.OffsetFrames
        && IntParam == o.IntParam && FloatParam == o.FloatParam
        && TrackId == o.TrackId && Priority == o.Priority;
    public override bool Equals(object? obj) => obj is Event o && Equals(o);
    public override int GetHashCode()
        => System.HashCode.Combine(Kind, OffsetFrames, IntParam, FloatParam, TrackId, Priority);
    public static bool operator ==(Event a, Event b) => a.Equals(b);
    public static bool operator !=(Event a, Event b) => !a.Equals(b);
}
