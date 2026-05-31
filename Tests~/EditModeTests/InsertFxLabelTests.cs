// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.IO;
using NUnit.Framework;

namespace Signo.Tests.UI;

[TestFixture]
public class InsertFxLabelTests
{
    static string XamlPath => Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml");

    [Test]
    public void ChrPedal_SendKnob_ShowsLevel_NotSend()
    {
        string xaml = File.ReadAllText(XamlPath);
        int valChSend = xaml.IndexOf("ValChSend", StringComparison.Ordinal);
        Assert.That(valChSend, Is.GreaterThan(0), "ValChSend must exist.");
        // Find the label near ValChSend
        string nearby = xaml.Substring(valChSend, Math.Min(300, xaml.Length - valChSend));
        Assert.That(nearby, Does.Contain("LEVEL"),
            "LEVEL label must appear near ValChSend.");
        Assert.That(nearby, Does.Not.Contain(">SEND<"),
            "SEND label must not appear near ValChSend (it is now LEVEL).");
    }
}
