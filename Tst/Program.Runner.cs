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
    static void RunnerFailureCapture()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "R3Test could not resolve its executable path for the runner failure probe.");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--runner-failure-probe");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "R3Test runner failure probe did not start.");
        if (!process.WaitForExit(milliseconds: 10_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                "R3Test runner failure probe hung instead of reporting a controlled failure.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        Assert(process.ExitCode != 0,
            "Expected the runner failure probe to return a non-zero exit code.");
        Assert(error.Contains(
                   "[FAIL] runner-failure-probe",
                   StringComparison.Ordinal) &&
               error.Contains(
                   "System.InvalidOperationException: Intentional R3Test runner failure probe.",
                   StringComparison.Ordinal) &&
               error.Contains("Stack:", StringComparison.Ordinal),
            "Expected the runner failure probe to report the actual exception type, message, and stack without a Windows crash dialog. " +
            $"stdout='{output}' stderr='{error}'");

        Console.WriteLine("R3Test controlled-failure runner regression PASS.");
    }

}
