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
    public void TimeDiv_Centre50_Is15ms()
        => Assert.That(LogMap(50, 5.0, 45.0), Is.EqualTo(15.0).Within(0.1));

    [Test]
    public void TimeDiv_Min0_Is5ms()
        => Assert.That(LogMap(0, 5.0, 45.0), Is.EqualTo(5.0).Within(0.01));

    [Test]
    public void TimeDiv_Max100_Is45ms()
        => Assert.That(LogMap(100, 5.0, 45.0), Is.EqualTo(45.0).Within(0.01));

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
