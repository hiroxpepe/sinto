#nullable enable
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class EnvelopeParamsTests
{
    [Test] public void EnvelopeParams_IsValueType()
        => Assert.That(typeof(EnvelopeParams).IsValueType, Is.True);

    [Test] public void Constructor_ValidValues_Stored()
    {
        var p = new EnvelopeParams(0.1f, 0.2f, 0.7f, 0.5f);
        Assert.That(p.Attack,  Is.EqualTo(0.1f).Within(1e-6f));
        Assert.That(p.Decay,   Is.EqualTo(0.2f).Within(1e-6f));
        Assert.That(p.Sustain, Is.EqualTo(0.7f).Within(1e-6f));
        Assert.That(p.Release, Is.EqualTo(0.5f).Within(1e-6f));
    }

    [TestCase(0.0f,   0.001f)] // below minimum → clamped to 0.001
    [TestCase(11.0f, 10.0f)]   // above maximum → clamped to 10.0
    public void Constructor_Attack_IsClamped(float input, float expected)
    {
        var p = new EnvelopeParams(input, 0.1f, 0.8f, 0.2f);
        Assert.That(p.Attack, Is.EqualTo(expected).Within(1e-4f));
    }

    [TestCase(-0.1f, 0.0f)]
    [TestCase(1.1f,  1.0f)]
    public void Constructor_Sustain_IsClamped(float input, float expected)
    {
        var p = new EnvelopeParams(0.01f, 0.1f, input, 0.2f);
        Assert.That(p.Sustain, Is.EqualTo(expected).Within(1e-6f));
    }

    [Test] public void Default_HasReasonableValues()
    {
        Assert.That(EnvelopeParams.Default.Attack,  Is.GreaterThan(0f));
        Assert.That(EnvelopeParams.Default.Release, Is.GreaterThan(0f));
    }

    [Test] public void Percussive_SustainIsZero()
        => Assert.That(EnvelopeParams.Percussive.Sustain, Is.EqualTo(0f).Within(1e-6f));

    [Test] public void Pad_AttackIsLong()
        => Assert.That(EnvelopeParams.Pad.Attack, Is.GreaterThan(0.3f));
}
