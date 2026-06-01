// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;

namespace Signo.Tests.Scope;

/// <summary>
/// Oscilloscope log-mapped slider conversions.
/// Slider 0..100, centre (50) = geometric mean = master's preferred default.
/// TIME/DIV: 5ms..45ms, centre = 15ms.
/// DCO level: 0.2..2.5, centre = 0.707 ≈ ×0.7.
/// </summary>
[TestFixture]
public class ScopeCalcTests
{
    static double LogMap(double v, double min, double max)
        => min * Math.Pow(max / min, v / 100.0);

    [Test]
    public void TimeDiv_Centre50_Is1Point8ms()
        => Assert.That(LogMap(50, 0.6, 5.4), Is.EqualTo(1.8).Within(0.1));

    [Test]
    public void TimeDiv_Min0_Is0Point6ms()
        => Assert.That(LogMap(0, 0.6, 5.4), Is.EqualTo(0.6).Within(0.01));

    [Test]
    public void TimeDiv_Max100_Is5Point4ms()
        => Assert.That(LogMap(100, 0.6, 5.4), Is.EqualTo(5.4).Within(0.01));

    [Test]
    public void DcoLevel_Centre50_IsPoint7()
        => Assert.That(LogMap(50, 0.2, 2.5), Is.EqualTo(0.707).Within(0.001));

    [Test]
    public void DcoLevel_Min0_IsPoint2()
        => Assert.That(LogMap(0, 0.2, 2.5), Is.EqualTo(0.2).Within(0.001));

    [Test]
    public void DcoLevel_Max100_Is2Point5()
        => Assert.That(LogMap(100, 0.2, 2.5), Is.EqualTo(2.5).Within(0.001));
}
