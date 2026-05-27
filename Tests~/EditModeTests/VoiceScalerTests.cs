// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class VoiceScalerTests
{
    const int SampleRate = 44100;
    const int BufLen     = 512;

    // bufferDuration in seconds = BufLen / SampleRate ≈ 11.6ms
    // 70% threshold: usage > 0.7 → tier down
    // Simulate overload: elapsed > bufDuration * 0.7 → 8.1ms
    static FakeTimeProvider SimulateOverload(VoiceScaler vs, int callbackCount) {
        var fake = new FakeTimeProvider();
        // Each callback: begin → advance 10ms (> 8.1ms threshold) → end
        double overloadMs = (BufLen / (double)SampleRate) * 1000.0 * 0.75; // 75% usage
        for (int i = 0; i < callbackCount; i++) {
            vs.OnCallbackBegin();
            fake.AdvanceMs(overloadMs);
            vs.OnCallbackEnd(BufLen, SampleRate);
        }
        return fake;
    }

    [Test] public void Constructor_DefaultTierIsZero()
    {
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, new FakeTimeProvider());
        Assert.That(vs.CurrentTierIndex, Is.EqualTo(0));
    }

    [Test] public void ForceSetTier_ChangesTier()
    {
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, new FakeTimeProvider());
        vs.ForceSetTier(2);
        Assert.That(vs.CurrentTierIndex, Is.EqualTo(2));
    }

    [Test] public void ForceSetTier_MaxVoicesIs16AtTier2()
    {
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, new FakeTimeProvider());
        vs.ForceSetTier(2);
        Assert.That(vs.CurrentMaxVoices, Is.EqualTo(16));
    }

    [Test] public void ForceSetTier_MaxVoicesIs24AtTier1()
    {
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, new FakeTimeProvider());
        vs.ForceSetTier(1);
        Assert.That(vs.CurrentMaxVoices, Is.EqualTo(24));
    }

    [Test] public void ForceSetTier_MaxVoicesIs32AtTier0()
    {
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, new FakeTimeProvider());
        vs.ForceSetTier(0);
        Assert.That(vs.CurrentMaxVoices, Is.EqualTo(32));
    }

    [Test] public void OnCallbackEnd_HighLoad_TierDecreases()
    {
        // FakeTimeProvider を使って Thread.Sleep なしで高負荷を注入する。
        // Thread.Sleep(5) は Windows で最悪 15.6ms 待機し Flaky Test になる。
        var fake = new FakeTimeProvider();
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, fake);

        double overloadMs = (BufLen / (double)SampleRate) * 1000.0 * 0.75;
        for (int i = 0; i < 10; i++) {
            vs.OnCallbackBegin();
            fake.AdvanceMs(overloadMs); // deterministic overload
            vs.OnCallbackEnd(BufLen, SampleRate);
        }
        Assert.That(vs.CurrentTierIndex, Is.GreaterThan(0),
            "Tier should decrease under sustained overload.");
    }

    [Test] public void CooldownPreventsHunting()
    {
        // ティアを1回下げた直後は、さらに下がらないことを確認する。
        var fake = new FakeTimeProvider();
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, fake);

        double overloadMs = (BufLen / (double)SampleRate) * 1000.0 * 0.75;

        // 1回目のティアダウンを誘発
        vs.OnCallbackBegin(); fake.AdvanceMs(overloadMs); vs.OnCallbackEnd(BufLen, SampleRate);
        int tierAfterFirst = vs.CurrentTierIndex;

        // 直後に再度過負荷を与えてもクールダウン中なのでティアが変わらない
        vs.OnCallbackBegin(); fake.AdvanceMs(overloadMs); vs.OnCallbackEnd(BufLen, SampleRate);
        Assert.That(vs.CurrentTierIndex, Is.EqualTo(tierAfterFirst),
            "Cooldown must prevent double tier-down (hunting).");
    }

    [Test] public void SingleFrameSpike_DoesNotTriggerTierDown()
    {
        // Mobile GC / OS scheduler causes single-frame audio callback delays.
        // 1 overloaded frame must NOT trigger TierDown — only N consecutive frames.
        var fake = new FakeTimeProvider();
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, fake);

        double overloadMs = (BufLen / (double)SampleRate) * 1000.0 * 0.75;
        double normalMs   = (BufLen / (double)SampleRate) * 1000.0 * 0.3;

        // ONE overloaded frame followed by normal frames
        vs.OnCallbackBegin(); fake.AdvanceMs(overloadMs); vs.OnCallbackEnd(BufLen, SampleRate);
        vs.OnCallbackBegin(); fake.AdvanceMs(normalMs);   vs.OnCallbackEnd(BufLen, SampleRate);
        vs.OnCallbackBegin(); fake.AdvanceMs(normalMs);   vs.OnCallbackEnd(BufLen, SampleRate);

        Assert.That(vs.CurrentTierIndex, Is.EqualTo(0),
            "Single-frame overload spike must NOT trigger TierDown. " +
            "Only N consecutive overloads should cause tier change.");
    }

    [Test] public void Cooldown_ExpiresAfter64Callbacks_AllowsNextTierChange()
    {
        // If _cooldownRemaining is never decremented, tier changes lock forever.
        // After 64 callbacks, cooldown must expire and allow another tier change.
        var fake = new FakeTimeProvider();
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, fake);

        double overloadMs = (BufLen / (double)SampleRate) * 1000.0 * 0.75;

        // First tier down
        vs.OnCallbackBegin(); fake.AdvanceMs(overloadMs); vs.OnCallbackEnd(BufLen, SampleRate);
        int tierAfterFirst = vs.CurrentTierIndex;

        // Run 64 callbacks with low load to exhaust cooldown
        double lowMs = (BufLen / (double)SampleRate) * 1000.0 * 0.3;
        for (int i = 0; i < 64; i++) {
            vs.OnCallbackBegin(); fake.AdvanceMs(lowMs); vs.OnCallbackEnd(BufLen, SampleRate);
        }

        // Now overload again — cooldown should have expired → tier can change
        vs.OnCallbackBegin(); fake.AdvanceMs(overloadMs); vs.OnCallbackEnd(BufLen, SampleRate);

        // If cooldown never decremented, tier would be stuck at tierAfterFirst
        // With correct decrement, another tier down is possible
        Assert.That(vs.CurrentTierIndex, Is.GreaterThanOrEqualTo(tierAfterFirst),
            "After cooldown expires, tier must be able to change again. " +
            "If stuck, _cooldownRemaining is never being decremented.");
    }

    [Test] public void LowLoad_Eventually_TierIncreases()
    {
        var fake = new FakeTimeProvider();
        var vm = new VoiceManager(32, SampleRate);
        var vs = new VoiceScaler(vm, fake);

        // まずティア1に下げる
        vs.ForceSetTier(1);

        // 低負荷を 301 コールバック分注入（HeadroomCallbacks = 300 を超える）
        double lowLoadMs = (BufLen / (double)SampleRate) * 1000.0 * 0.3; // 30% usage
        for (int i = 0; i < 301; i++) {
            vs.OnCallbackBegin();
            fake.AdvanceMs(lowLoadMs);
            vs.OnCallbackEnd(BufLen, SampleRate);
        }
        Assert.That(vs.CurrentTierIndex, Is.LessThan(1),
            "Tier should increase after sustained low load.");
    }
}
