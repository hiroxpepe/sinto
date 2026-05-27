#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Sinto.Core.Effects;

public abstract class MonoEffect : IEffect
{
    public bool MonoCompatible { get; set; }
    public bool Enabled        { get; set; }
    public abstract void Process(Span<float> buffer, int channels);
    public abstract void Reset();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ApplyMonoCompatibility(Span<float> buffer, int channels)
        => throw new NotImplementedException();
}
