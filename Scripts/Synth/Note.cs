// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Sinto.Core.Synth;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Note : System.IEquatable<Note> {
    public readonly int   MidiNote;
    public readonly float Velocity;
    public readonly int   TrackId;
    public readonly int   Priority;

    public Note(int midiNote, float velocity, int trackId, int priority)
        => throw new System.NotImplementedException();

    public float FrequencyHz => throw new System.NotImplementedException();

    public bool Equals(Note o) => throw new System.NotImplementedException();
    public override bool Equals(object? obj) => throw new System.NotImplementedException();
    public override int GetHashCode() => throw new System.NotImplementedException();
}
