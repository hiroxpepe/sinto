#nullable enable
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Sinto.Core.Audio;

namespace Sinto.Tests.Audio;

[TestFixture]
public class DenormalGuardTests
{
    [Test]
    public void Protect_EvenIndex_ReturnsPositiveOffset()
    {
        float result = DenormalGuard.Protect(0.0f, 0L);
        Assert.That(result, Is.GreaterThan(0f));
    }

    [Test]
    public void Protect_OddIndex_ReturnsNegativeOffset()
    {
        float result = DenormalGuard.Protect(0.0f, 1L);
        Assert.That(result, Is.LessThan(0f));
    }

    [Test]
    public void Protect_AlternatingSigns_CancelDCBias()
    {
        double sum = 0;
        for (long i = 0; i < 1000; i++)
            sum += DenormalGuard.Protect(0.0f, i);
        Assert.That(sum, Is.EqualTo(0.0).Within(1e-12));
    }

    [Test]
    public void Protect_Subnormal_IsNoLongerSubnormal()
    {
        float subnormal = 1e-40f;
        float result = DenormalGuard.Protect(subnormal, 0L);
        Assert.That(DenormalGuard.IsDenormal(result), Is.False);
    }

    [Test]
    public void Protect_NormalValue_IsNotSubnormal()
    {
        float result = DenormalGuard.Protect(1.0f, 0L);
        Assert.That(DenormalGuard.IsDenormal(result), Is.False);
    }

    [Test]
    public void IsDenormal_Zero_ReturnsFalse()
        => Assert.That(DenormalGuard.IsDenormal(0.0f), Is.False);

    [Test]
    public void IsDenormal_Subnormal_ReturnsTrue()
        => Assert.That(DenormalGuard.IsDenormal(1e-40f), Is.True);

    [Test]
    public void IsDenormal_Normal_ReturnsFalse()
        => Assert.That(DenormalGuard.IsDenormal(1.0f), Is.False);

    [Test]
    public void Protect_NoStaticState_ThreadSafe()
    {
        // 8スレッドが同時に呼んでもデータレースが起きないこと
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var tasks = new Task[8];
        for (int t = 0; t < 8; t++) {
            int tid = t;
            tasks[t] = Task.Run(() => {
                try {
                    for (long i = 0; i < 10000; i++)
                        DenormalGuard.Protect(0.0f, i + tid * 10000);
                } catch (Exception ex) { exceptions.Add(ex); }
            });
        }
        Task.WaitAll(tasks);
        Assert.That(exceptions, Is.Empty);
    }
}
