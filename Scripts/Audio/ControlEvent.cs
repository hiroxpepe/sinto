// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Sinto.Core.Audio;

public enum ControlEventKind : byte {
    None         = 0,  // MUST be 0 — default(ControlEvent).Kind is None, not NoteOn
    NoteOn       = 1,
    NoteOff      = 2,
    Pause        = 3,
    Resume       = 4,
    SetVoiceLimit = 5,
    SwapPreset   = 6,
    SetBPM       = 7,
    SustainPedal = 8,  // CC64: FloatParam=1.0 down, 0.0 up
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct ControlEvent : System.IEquatable<ControlEvent> {
    public readonly ControlEventKind Kind;
    public readonly ushort OffsetFrames;
    public readonly int    IntParam;
    public readonly float  FloatParam;
    public readonly int    TrackId;
    public readonly int    Priority;

    public ControlEvent(ControlEventKind kind, ushort offsetFrames = 0,
        int intParam = 0, float floatParam = 0f, int trackId = 0, int priority = 0) {
        Kind = kind; OffsetFrames = offsetFrames;
        IntParam = intParam; FloatParam = floatParam;
        TrackId = trackId; Priority = priority;
    }
    public bool Equals(ControlEvent o)
        => Kind == o.Kind && OffsetFrames == o.OffsetFrames
        && IntParam == o.IntParam && FloatParam == o.FloatParam
        && TrackId == o.TrackId && Priority == o.Priority;
    public override bool Equals(object? obj) => obj is ControlEvent o && Equals(o);
    public override int GetHashCode()
        => System.HashCode.Combine(Kind, OffsetFrames, IntParam, FloatParam, TrackId, Priority);
    public static bool operator ==(ControlEvent a, ControlEvent b) => a.Equals(b);
    public static bool operator !=(ControlEvent a, ControlEvent b) => !a.Equals(b);
}
