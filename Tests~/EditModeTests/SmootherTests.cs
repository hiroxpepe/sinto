#nullable enable
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class SmootherTests
{
    [Test] public void Constructor_SetsCurrentToInitialValue()
    {
        var sp = new Smoother(0.5f);
        Assert.That(sp.current, Is.EqualTo(0.5f).Within(1e-6f));
    }

    [Test] public void Constructor_SetsTargetToInitialValue()
    {
        var sp = new Smoother(0.3f);
        Assert.That(sp.target, Is.EqualTo(0.3f).Within(1e-6f));
    }

    [Test] public void SnapToTarget_SetsCurrentToTarget()
    {
        var sp = new Smoother(0.0f);
        sp.SetTarget(0.9f);
        sp.SnapToTarget();
        Assert.That(sp.current, Is.EqualTo(0.9f).Within(1e-6f));
    }

    [Test] public void Tick_ApproachesTarget()
    {
        var sp = new Smoother(0.0f);
        sp.SetTarget(1.0f);
        float prev = sp.current;
        sp.Tick();
        Assert.That(sp.current, Is.GreaterThan(prev));
    }

    [Test] public void Tick_NeverOvershoots_AndApproachesTarget()
    {
        // One-pole lowpass asymptotically approaches target: verify both non-overshoot and convergence.
        // LessThanOrEqualTo alone would pass even if current stays at 0.0 (false negative).
        var sp = new Smoother(0.0f, smoothingHz: 20f, sampleRate: 44100);
        sp.SetTarget(1.0f);
        for (int i = 0; i < 10000; i++) sp.Tick(); // ~226ms @ 44100Hz >> 8ms response time
        // Must not overshoot
        Assert.That(sp.current, Is.LessThanOrEqualTo(1.0f + 1e-6f),
            "Smoother must not overshoot target.");
        // Must be close enough to target (10000 samples ≈ 226ms should reach 99.9%+)
        Assert.That(sp.current, Is.GreaterThanOrEqualTo(1.0f - 1e-3f),
            "Smoother must have converged to within 0.1% of target after 226ms.");
    }

    [Test] public void SetTarget_SameValue_CurrentStable()
    {
        var sp = new Smoother(0.5f);
        sp.SetTarget(0.5f);
        sp.SnapToTarget();
        float before = sp.Tick();
        Assert.That(before, Is.EqualTo(0.5f).Within(1e-4f));
    }

    [Test] public void SnapToTarget_OnNoteOn_NoPyunArtifact()
    {
        // Previous voice left Cutoff=0.1; new NoteOn requests 0.9
        var sp = new Smoother(0.1f);
        sp.SetTarget(0.9f);
        sp.SnapToTarget(); // Always call on NoteOn
        // First sample must be 0.9 (no glide from 0.1)
        Assert.That(sp.current, Is.EqualTo(0.9f).Within(1e-6f));
    }

    [Test] public void Constructor_ZeroSampleRate_ThrowsArgumentException()
        => Assert.Throws<System.ArgumentException>(() => new Smoother(0f, 20f, 0));

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(-100f)]
    public void Constructor_ZeroOrNegativeSmoothingHz_ThrowsArgumentOutOfRangeException(float hz)
    {
        // smoothingHz=0 → coeff=0 → current never moves → parameter frozen forever.
        // Must throw to catch misconfiguration early.
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => new Smoother(0f, hz, 44100),
            $"smoothingHz={hz} must throw ArgumentOutOfRangeException.");
    }

    [Test] public void Tick_AfterConvergence_DoesNotProduceNaN()
    {
        // Smoother is a 1-pole IIR. After target is reached,
        // the difference (target - current) becomes subnormal → CPU spike.
        // Sleep optimization or Denormal must prevent NaN/subnormal output.
        var sp = new Smoother(0.0f, smoothingHz: 20f, sampleRate: 44100);
        sp.SetTarget(0.5f);
        for (int i = 0; i < 44100; i++) sp.Tick(); // run until convergence
        float result = sp.Tick();
        Assert.That(float.IsNaN(result),      Is.False, "Tick() must never return NaN.");
        Assert.That(float.IsInfinity(result), Is.False, "Tick() must never return Infinity.");
        Assert.That(result, Is.EqualTo(0.5f).Within(1e-4f), "Must converge to target.");
    }

    [Test] public void Tick_AfterConvergence_DoesNotTriggerGC()
    {
        // Verify no allocation in steady-state (converged) tick loop.
        // Force a clean GC state first so we measure only what THIS loop allocates.
        var sp = new Smoother(1.0f, smoothingHz: 20f, sampleRate: 44100);
        sp.SnapToTarget();
        // Warm up: ensure any JIT/init allocations happen BEFORE the measurement window
        for (int i = 0; i < 100; i++) sp.Tick();
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        int gen0Before = System.GC.CollectionCount(0);
        for (int i = 0; i < 44100; i++) sp.Tick();
        Assert.That(System.GC.CollectionCount(0), Is.EqualTo(gen0Before));
    }
}
