// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Signo.Core.Signal;

/// <summary>
/// Sound source abstraction. Implemented by VAEngine, PCMEngine, FMEngine.
/// </summary>
public interface ISynth : ISignal
{
    bool SendNoteOn(int midiNote, float velocity, int trackId, int priority, ushort offsetFrames);
    bool SendNoteOff(int midiNote, int trackId, ushort offsetFrames);
}
