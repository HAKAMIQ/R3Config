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
    static void ListFind()
    {
        var empty = new ListVm();
        Assert(
            empty.LibraryScanState == GameLibraryScanState.NotStarted &&
            empty.EmptyTxt == UiText.GetText("Games.LibraryPathRequired"),
            "Expected a fresh game list to report that the game-library scan has not started instead of claiming that no games exist.");

        empty.BeginLibraryScan();
        Assert(
            empty.LibraryScanState == GameLibraryScanState.Scanning &&
            empty.EmptyTxt == UiText.GetText("Games.LibraryScanning"),
            "Expected an active automatic game-library scan to expose a scanning state.");

        empty.FailLibraryScan();
        Assert(
            empty.LibraryScanState == GameLibraryScanState.Failed &&
            empty.EmptyTxt == UiText.GetText("Games.LibraryScanFailed"),
            "Expected a failed game-library scan to remain distinct from a completed empty scan.");

        empty.Load([]);
        Assert(
            empty.LibraryScanState == GameLibraryScanState.Completed &&
            empty.EmptyTxt == UiText.GetText("Games.EmptyInstalled"),
            "Expected the no-games message only after the game-library scan completed with zero detected games.");

        var list = new ListVm();
        var collectionChanges = 0;
        NotifyCollectionChangedAction? lastCollectionAction = null;
        list.Games.CollectionChanged += (_, args) =>
        {
            collectionChanges++;
            lastCollectionAction = args.Action;
        };
        list.Load(
        [
            MakeGame("NPEA00137", ".detuned", isDetectedInRpcs3: true),
            MakeGame("BLUS30443", "Demon's Souls", isDetectedInRpcs3: false),
            MakeGame("BCES00569", "Gran Turismo 5", isDetectedInRpcs3: true)
        ]);

        Assert(list.Games.Count == 2, "Expected detected-only filter to show detected games by default.");
        Assert(collectionChanges == 1 &&
               lastCollectionAction == NotifyCollectionChangedAction.Reset,
            "Expected game-list replacement to publish one reset instead of one UI notification per row.");
        Assert(list.IsSearch, "Expected initial game search to be empty.");

        list.Text = "gran";
        Assert(list.Games.Count == 1 && list.Games[0].Serial == "BCES00569", "Expected game search to match title.");

        list.Text = "NPEA00137";
        Assert(list.Games.Count == 1 && list.Games[0].Title == ".detuned", "Expected game search to match serial.");

        list.Text = "souls";
        Assert(list.Games.Count == 0 && list.EmptyTxt == UiText.GetText("Games.EmptySearch"),
            "Expected search with no visible detected result to show search empty state.");

        list.InR3 = false;
        Assert(list.Games.Count == 1 && list.Games[0].Serial == "BLUS30443",
            "Expected search to include catalog-only games when detected-only filter is disabled.");

        var installedDescriptor = new Rpcs3GameDescriptor(
            "BLUS30001",
            "Installed PARAM.SFO title",
            @"C:\Games\BLUS30001",
            "01.00",
            null,
            null);
        var conflictingCatalog = new WikiRec(
            "BLUS30001",
            "Different catalog title",
            []);
        Assert(GameVm.ResolveTitle(
                   installedDescriptor,
                   conflictingCatalog) ==
               "Installed PARAM.SFO title",
            "Expected the installed PARAM.SFO title to take precedence over catalog display text.");

        var serialFallback = installedDescriptor with
        {
            Title = installedDescriptor.Serial
        };
        Assert(GameVm.ResolveTitle(
                   serialFallback,
                   conflictingCatalog) ==
               "Different catalog title",
            "Expected the catalog title to be used only when installed metadata has no usable title.");

        Console.WriteLine("Game list search/filter smoke PASS.");
    }

    static GameVm MakeGame(string serial, string title, bool isDetectedInRpcs3)
    {
        var descriptor = new Rpcs3GameDescriptor(serial, title, @"C:\Games\" + serial, "01.00", null, null);
        var summary = new RecSum(serial, 0, 0, 0, 0, 0, 0, []);
        return new GameVm(descriptor, null, summary, serial, title, isDetectedInRpcs3);
    }

    static AppFlow TestFlow()
    {
        return new AppFlow(
            AppComposition.Build(
                new Rpcs3MutationGuard(() => false)));
    }

}
