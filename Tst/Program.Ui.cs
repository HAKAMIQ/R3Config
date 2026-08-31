using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

internal static partial class Program
{
    static void Ui_Layout()
    {
        var arabic = UiLaySt.FromLang(UiText.ArabLang);
        Assert(arabic.IsRtl, "Expected ar-SA to use the native Windows RTL layout.");
        Assert(arabic.WinFlow == FlowDirection.RightToLeft &&
               arabic.ShFlow == FlowDirection.RightToLeft &&
               arabic.TextFlow == FlowDirection.RightToLeft,
            "Expected Arabic window, shell, and localized text geometry to use RTL.");
        Assert(arabic.TechFlow == FlowDirection.LeftToRight &&
               arabic.TxtAlign == TextAlignment.Left &&
               arabic.SetAlign == TextAlignment.Left &&
               arabic.TechAlgn == TextAlignment.Left &&
               arabic.HorAlign == HorizontalAlignment.Right &&
               arabic.TrailAlign == HorizontalAlignment.Left,
            "Expected Arabic layout to be RTL while localized and technical text remain left-aligned, with actions trailing on the left.");
        Assert(arabic.GameFlow(hasSelectedGame: true) == FlowDirection.LeftToRight &&
               arabic.GameFlow(hasSelectedGame: false) == FlowDirection.RightToLeft,
            "Expected selected game identities to stay technical while localized empty text follows Arabic.");

        var english = UiLaySt.FromLang(UiText.EnLang);
        Assert(!english.IsRtl &&
               english.WinFlow == FlowDirection.LeftToRight &&
               english.ShFlow == FlowDirection.LeftToRight &&
               english.TextFlow == FlowDirection.LeftToRight &&
               english.TxtAlign == TextAlignment.Left &&
               english.SetAlign == TextAlignment.Left &&
               english.TechAlgn == TextAlignment.Left &&
               english.HorAlign == HorizontalAlignment.Left &&
               english.TrailAlign == HorizontalAlignment.Right,
            "Expected English shell and text to remain LTR, with actions trailing on the right.");
    }

}
