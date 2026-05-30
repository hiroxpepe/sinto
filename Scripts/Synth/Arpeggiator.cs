// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Collections.Generic;

namespace Signo.Core.Synth;

/// <summary>
/// Arpeggiator over held notes. Modes UP / DOWN / UP-DOWN / RANDOM. Held notes
/// are kept sorted ascending; NextStep() advances one step and returns the MIDI
/// note to play (-1 when nothing is held). RANDOM uses a small seedable LCG so
/// patterns are deterministic in tests.
/// </summary>
public sealed class Arpeggiator {
#nullable enable
    readonly List<int> _held = new();
    ArpMode _mode = ArpMode.Up;
    int  _index;       // current position in the pattern
    int  _dir = 1;     // for UpDown: +1 ascending, -1 descending
    uint _rng = 0x9E3779B9u;

    public void SetMode(ArpMode mode) {
        _mode = mode;
        _index = 0;
        _dir = 1;
    }

    public void SetSeed(uint seed) => _rng = seed == 0 ? 1u : seed;

    public void NoteOn(int midi) {
        if (_held.Contains(midi)) return;
        _held.Add(midi);
        _held.Sort();
    }

    public void NoteOff(int midi) {
        _held.Remove(midi);
        if (_index >= _held.Count) _index = 0;
    }

    public int HeldCount => _held.Count;

    /// <summary>Advance one step and return the MIDI note to play (-1 if none held).</summary>
    public int NextStep() {
        int n = _held.Count;
        if (n == 0) return -1;
        if (n == 1) return _held[0];

        switch (_mode) {
            case ArpMode.Up: {
                int note = _held[_index % n];
                _index = (_index + 1) % n;
                return note;
            }
            case ArpMode.Down: {
                // start from the top
                int idx = (n - 1) - (_index % n);
                _index = (_index + 1) % n;
                return _held[idx];
            }
            case ArpMode.UpDown: {
                int note = _held[_index];
                // bounce without doubling the ends
                if (_dir > 0) {
                    if (_index >= n - 1) { _dir = -1; _index--; }
                    else _index++;
                } else {
                    if (_index <= 0) { _dir = 1; _index++; }
                    else _index--;
                }
                return note;
            }
            case ArpMode.Random: {
                // xorshift LCG
                _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
                int idx = (int)(_rng % (uint)n);
                return _held[idx];
            }
            default:
                return _held[0];
        }
    }

    public void Clear() {
        _held.Clear();
        _index = 0;
        _dir = 1;
    }
}
