#nullable enable
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class EnvelopeTests
{
    const int SR = 44100;

    [Test] public void Initial_PhaseIsFree()
    {
        var e = new Envelope();
        Assert.That(e.Phase, Is.EqualTo(PlayState.Free));
    }

    [Test] public void NoteOn_PhaseBecomesAttack()
    {
        var e = new Envelope();
        e.NoteOn(EnvParams.Default, SR);
        Assert.That(e.Phase, Is.EqualTo(PlayState.Attack));
    }

    [Test] public void Attack_LevelIncreasesEachTick()
    {
        var e = new Envelope();
        e.NoteOn(new EnvParams(1.0f, 0.1f, 0.8f, 0.2f), SR);
        float prev = e.Level;
        e.Tick();
        Assert.That(e.Level, Is.GreaterThan(prev));
    }

    [Test] public void Attack_ReachesDecayPhase()
    {
        var e = new Envelope();
        e.NoteOn(new EnvParams(0.001f, 0.1f, 0.8f, 0.2f), SR);
        for (int i = 0; i < SR; i++) { e.Tick(); if (e.Phase == PlayState.Decay) break; }
        Assert.That(e.Phase, Is.EqualTo(PlayState.Decay));
    }

    [Test] public void Decay_ReachesSustainPhase()
    {
        var e = new Envelope();
        e.NoteOn(new EnvParams(0.001f, 0.001f, 0.5f, 0.2f), SR);
        for (int i = 0; i < SR * 2; i++) { e.Tick(); if (e.Phase == PlayState.Sustain) break; }
        Assert.That(e.Phase, Is.EqualTo(PlayState.Sustain));
    }

    [Test] public void NoteOff_TransitionsToRelease()
    {
        var e = new Envelope();
        e.NoteOn(EnvParams.Default, SR);
        for (int i = 0; i < SR / 10; i++) e.Tick();
        e.NoteOff();
        Assert.That(e.Phase, Is.EqualTo(PlayState.Release));
    }

    [Test] public void Release_IsDoneWhenLevelReachesZero()
    {
        var e = new Envelope();
        e.NoteOn(new EnvParams(0.001f, 0.001f, 0.0f, 0.001f), SR);
        for (int i = 0; i < SR; i++) e.Tick();
        e.NoteOff();
        for (int i = 0; i < SR; i++) { e.Tick(); if (e.IsDone) break; }
        Assert.That(e.IsDone, Is.True);
    }

    [Test] public void QuickRelease_PhaseBecomesQuickRelease()
    {
        var e = new Envelope();
        e.NoteOn(EnvParams.Default, SR);
        for (int i = 0; i < 100; i++) e.Tick();
        e.StartQuickRelease(SR);
        Assert.That(e.Phase, Is.EqualTo(PlayState.QuickRelease));
    }

    [Test] public void QuickRelease_CompletesWithin220Samples()
    {
        var e = new Envelope();
        e.NoteOn(new EnvParams(0.001f, 0.001f, 1.0f, 0.001f), SR);
        for (int i = 0; i < 200; i++) e.Tick(); // sustain phase
        e.StartQuickRelease(SR);
        for (int i = 0; i < 220; i++) { e.Tick(); if (e.IsDone) break; }
        Assert.That(e.IsDone, Is.True);
    }

    [Test] public void NoteOff_DuringAttack_TransitionsToRelease()
    {
        var e = new Envelope();
        e.NoteOn(new EnvParams(10.0f, 0.1f, 0.8f, 0.2f), SR); // long attack
        for (int i = 0; i < 10; i++) e.Tick();
        e.NoteOff();
        Assert.That(e.Phase, Is.EqualTo(PlayState.Release));
    }

    [Test] public void SustainZero_DecayReachesZero()
    {
        var e = new Envelope();
        e.NoteOn(new EnvParams(0.001f, 0.001f, 0.0f, 0.001f), SR);
        for (int i = 0; i < SR; i++) { e.Tick(); if (e.Phase == PlayState.Sustain) break; }
        Assert.That(e.Level, Is.EqualTo(0f).Within(1e-4f));
    }

    [Test] public void NoteOn_DefaultEnvParams_DoesNotProduceNaN()
    {
        // default(EnvParams) bypasses the constructor → all fields = 0.0f.
        // Without internal clamping in NoteOn, rate = 1/(0 * SR) = NaN → silent audio thread.
        var e = new Envelope();
        Assert.DoesNotThrow(() => e.NoteOn(default(EnvParams), SR),
            "NoteOn must not throw when given default(EnvParams).");
        float result = e.Tick();
        Assert.That(float.IsNaN(result),      Is.False, "Tick() after default params must not return NaN.");
        Assert.That(float.IsInfinity(result), Is.False, "Tick() after default params must not return Inf.");
    }

    [Test] public void NoteOn_ZeroAttack_InstantlyAtFullLevel()
    {
        // Zero attack = instant full level. Valid for SFX (explosions, drum hits).
        // WRONG: clamp to 0.001 → adds 1ms click artifact, corrupts sound design.
        // CORRECT: bypass attack phase entirely → _level = 1.0, jump to Decay.
        var e = new Envelope();
        var zeroAttack = new EnvParams(0.0f, 0.1f, 0.8f, 0.2f);
        e.NoteOn(zeroAttack, SR);

        // First tick must NOT return NaN
        float first = e.Tick();
        Assert.That(float.IsNaN(first), Is.False, "Zero attack must not produce NaN.");
        Assert.That(float.IsInfinity(first), Is.False, "Zero attack must not produce Infinity.");

        // Level must be at or near 1.0 immediately (bypassed attack phase)
        Assert.That(e.Level, Is.GreaterThanOrEqualTo(0.9f),
            "Zero attack must produce near-full level immediately. " +
            "Do NOT clamp to 0.001 — that adds an unwanted 1ms click.");

        // Phase must have moved past Attack (Decay or Sustain)
        Assert.That(e.Phase, Is.Not.EqualTo(PlayState.Attack),
            "Zero attack must bypass Attack phase entirely.");
    }

    [Test] public void NoteOn_DefaultEnvParams_BypassesAttackPhase()
    {
        // default(EnvParams) = all 0.0f — bypasses constructor.
        // Must NOT cause NaN. Must bypass attack and jump to Decay/Sustain.
        var e = new Envelope();
        e.NoteOn(default(EnvParams), SR);
        float result = e.Tick();
        Assert.That(float.IsNaN(result),      Is.False);
        Assert.That(float.IsInfinity(result), Is.False);
    }
}
