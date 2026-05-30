#nullable enable
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

[TestFixture]
public class TrackConfigTests
{
    [Test] public void TrackConfig_IsValueType()
        => Assert.That(typeof(TrackConfig).IsValueType, Is.True);

    [Test] public void Constructor_Values_AreStored()
    {
        var vc = new TrackConfig(4, 8, false);
        Assert.That(vc.ReservedVoices, Is.EqualTo(4));
        Assert.That(vc.Priority,       Is.EqualTo(8));
        Assert.That(vc.Protected,      Is.False);
    }

    [Test] public void DefaultConfigs_HasEightEntries()
        => Assert.That(TrackConfig.DefaultConfigs.Count, Is.EqualTo(8));

    [Test] public void DefaultConfigs_Track0_IsProtected()
        => Assert.That(TrackConfig.DefaultConfigs[0].Protected, Is.True);

    [Test] public void DefaultConfigs_Track1_IsProtected()
        => Assert.That(TrackConfig.DefaultConfigs[1].Protected, Is.True);

    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    [TestCase(5)] [TestCase(6)] [TestCase(7)]
    public void DefaultConfigs_NonDrumTracks_AreNotProtected(int trackId)
        => Assert.That(TrackConfig.DefaultConfigs[trackId].Protected, Is.False);

    // ── GetConfig: O(1) switch for audio hot path ────────────────

    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(7, false)]
    public void GetConfig_ValidTrackId_ReturnsCorrectProtected(int trackId, bool expected)
        => Assert.That(TrackConfig.GetConfig(trackId).Protected, Is.EqualTo(expected));

    [TestCase(8)]
    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MinValue)]
    public void GetConfig_InvalidTrackId_ReturnsSafeDefault(int trackId)
    {
        // Invalid trackId must not throw — returns lowest-priority non-protected config
        var cfg = TrackConfig.GetConfig(trackId);
        Assert.That(cfg.Protected, Is.False, "Invalid trackId must return non-protected config.");
        Assert.That(cfg.Priority, Is.EqualTo(1), "Invalid trackId must return lowest priority.");
    }
}
