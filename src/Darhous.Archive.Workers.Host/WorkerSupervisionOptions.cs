using System;

namespace Darhous.Archive.Workers.Host;

public sealed class WorkerSupervisionOptions
{
    public TimeSpan FirstRestartDelay { get; init; } = TimeSpan.Zero;
    public TimeSpan SecondRestartDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan ThirdRestartDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan CrashLoopWindow { get; init; } = TimeSpan.FromMinutes(10);
    public int CrashLoopThreshold { get; init; } = 3;
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}
