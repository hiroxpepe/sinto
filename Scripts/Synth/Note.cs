// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.InteropServices;

namespace Signo.Core.Synth;

/// <summary>MIDI note with cached frequency. Immutable.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Note : IEquatable<Note> {
#nullable enable
    public readonly int   MidiNote;
    public readonly float Velocity;
    public readonly int   TrackId;
    public readonly int   Priority;
    public readonly float FrequencyHz;

    public Note(int midi_note, float velocity, int track_id, int priority) {
        // Clamp MIDI note to [0, 127]
        if      (midi_note < 0)   midi_note = 0;
        else if (midi_note > 127) midi_note = 127;
        // Clamp velocity to [0, 1]
        if      (velocity < 0f) velocity = 0f;
        else if (velocity > 1f) velocity = 1f;
        MidiNote = midi_note;
        Velocity = velocity;
        TrackId  = track_id;
        Priority = priority;
        // Cache frequency: f = 440 × 2^((midi - 69) / 12)
        FrequencyHz = 440f * MathF.Pow(2f, (midi_note - 69) / 12f);
    }

    public bool Equals(Note o)
        => MidiNote == o.MidiNote && Velocity == o.Velocity
        && TrackId == o.TrackId && Priority == o.Priority;
    public override bool Equals(object? obj) => obj is Note o && Equals(o);
    public override int GetHashCode()
        => HashCode.Combine(MidiNote, Velocity, TrackId, Priority);
}
