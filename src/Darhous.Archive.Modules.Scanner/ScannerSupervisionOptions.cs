using System;
using System.Collections.Generic;

namespace Darhous.Archive.Modules.Scanner;

public sealed class ScannerSupervisionOptions
{
    public string ExecutablePath { get; set; } = "Darhous.Archive.Scanner.Worker.exe";
    public TimeSpan FirstRestartDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan SecondRestartDelay { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ThirdRestartDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CrashLoopWindow { get; set; } = TimeSpan.FromMinutes(10);
    public int CrashLoopThreshold { get; set; } = 3;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}
