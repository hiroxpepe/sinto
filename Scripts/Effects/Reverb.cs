#nullable enable
using System;

namespace Sinto.Core.Effects;

public sealed class Reverb : MonoEffect
{
    public float RoomSize { get; set; } = 0.5f;
    public float Damping  { get; set; } = 0.5f;
    public float Mix      { get; set; } = 0.3f;
    public override void Process(Span<float> buffer, int channels)
        => throw new NotImplementedException();
    public override void Reset()
        => throw new NotImplementedException();
}
