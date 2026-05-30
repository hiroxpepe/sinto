#nullable enable
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Signo.Core.Audio;

namespace Signo.Tests.Audio;

[TestFixture]
public class DenormalTests
{
    [Test]
    public void Protect_FirstCycle_ReturnsPositiveOffset()
    {
        // sampleIndex 0..255 → first 256-sample cycle → positive
        float result = Denormal.Protect(0.0f, 0L);
        Assert.That(result, Is.GreaterThan(0f));
    }

    [Test]
    public void Protect_SecondCycle_ReturnsNegativeOffset()
    {
        // sampleIndex 256..511 → second cycle → negative
        float result = Denormal.Protect(0.0f, 256L);
        Assert.That(result, Is.LessThan(0f));
    }

    [Test]
    public void Protect_SignFlipsEvery256Samples_NotEverySample()
    {
        // Per-sample alternation = Nyquist frequency = filtered out by LPF → subnormal survives.
        // Must flip every 256 samples to survive low-pass filtering.
        float first  = Denormal.Protect(0.0f, 0L);
        float second = Denormal.Protect(0.0f, 1L);
        // Within the same 256-sample cycle, sign must be SAME (not flipped)
        Assert.That(System.Math.Sign(first), Is.EqualTo(System.Math.Sign(second)),
            "Sign must NOT flip every sample (Nyquist problem). Must flip every 256 samples.");
        // After 256 samples, sign must flip
        float after256 = Denormal.Protect(0.0f, 256L);
        Assert.That(System.Math.Sign(first), Is.Not.EqualTo(System.Math.Sign(after256)),
            "Sign must flip after 256 samples.");
    }

    [Test]
    public void Protect_AlternatingSigns_CancelDCBias()
    {
        // Must sum to zero over complete cycles (512 samples = 2 full cycles)
        double sum = 0;
        for (long i = 0; i < 512; i++)
            sum += Denormal.Protect(0.0f, i);
        Assert.That(sum, Is.EqualTo(0.0).Within(1e-12));
    }

    [Test]
    public void Protect_Subnormal_IsNoLongerSubnormal()
    {
        float subnormal = 1e-40f;
        float result = Denormal.Protect(subnormal, 0L);
        Assert.That(Denormal.IsDenormal(result), Is.False);
    }

    [Test]
    public void Protect_NormalValue_IsNotSubnormal()
    {
        float result = Denormal.Protect(1.0f, 0L);
        Assert.That(Denormal.IsDenormal(result), Is.False);
    }

    [Test]
    public void IsDenormal_Zero_ReturnsFalse()
        => Assert.That(Denormal.IsDenormal(0.0f), Is.False);

    [Test]
    public void IsDenormal_Subnormal_ReturnsTrue()
        => Assert.That(Denormal.IsDenormal(1e-40f), Is.True);

    [Test]
    public void IsDenormal_Normal_ReturnsFalse()
        => Assert.That(Denormal.IsDenormal(1.0f), Is.False);

    [Test]
    public void Protect_NoStaticState_ThreadSafe()
    {
        // 8 threads calling simultaneously must not cause data races
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var tasks = new Task[8];
        for (int t = 0; t < 8; t++) {
            int tid = t;
            tasks[t] = Task.Run(() => {
                try {
                    for (long i = 0; i < 10000; i++)
                        Denormal.Protect(0.0f, i + tid * 10000);
                } catch (Exception ex) { exceptions.Add(ex); }
            });
        }
        Task.WaitAll(tasks);
        Assert.That(exceptions, Is.Empty);
    }
}
