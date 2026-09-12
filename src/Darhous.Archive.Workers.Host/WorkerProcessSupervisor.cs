using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Contracts.Health;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Time;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Workers.Host;

public sealed class WorkerProcessSupervisor : IHealthContributor, IAsyncDisposable
{
    private readonly string _executablePath;
    private readonly WorkerSupervisionOptions _options;
    private readonly IWorkerDelayProvider _delayProvider;
    private readonly IClock _clock;
    private readonly IHealthRegistry _healthRegistry;
    private readonly ILogger<WorkerProcessSupervisor> _logger;

    private Process? _process;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<DateTimeOffset> _recentCrashes = new();
    
    private string _status = "stopped";
    private int _crashCount = 0;
    private bool _disposed;
    private CancellationTokenSource? _runCts;
    private Task? _monitorTask;

    public string Component => $"Worker ({Path.GetFileNameWithoutExtension(_executablePath)})";
    public string CurrentStatus => _status;

    public WorkerProcessSupervisor(
        string executablePath,
        WorkerSupervisionOptions options,
        IWorkerDelayProvider delayProvider,
        IClock clock,
        IHealthRegistry healthRegistry,
        ILogger<WorkerProcessSupervisor> logger)
    {
        _executablePath = executablePath;
        _options = options;
        _delayProvider = delayProvider;
        _clock = clock;
        _healthRegistry = healthRegistry;
        _logger = logger;
    }

    public Task<HealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        var status = _status switch
        {
            "healthy" => HealthStatus.Healthy,
            "starting" => HealthStatus.Healthy,
            "degraded" => HealthStatus.Degraded,
            "failed" => HealthStatus.Failed,
            "quarantined" => HealthStatus.Failed,
            _ => HealthStatus.Failed
        };

        return Task.FromResult(new HealthReport(Component, status, $"Status: {_status}, Crashes: {_crashCount}", _clock.UtcNow));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_status is not "stopped" and not "quarantined" and not "failed")
            {
                throw new InvalidOperationException($"Cannot start from status {_status}");
            }

            _status = "starting";
            _healthRegistry.Register(this);
            
            _runCts = new CancellationTokenSource();
            _monitorTask = RunMonitorLoopAsync(_runCts.Token);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_runCts != null)
            {
                _runCts.Cancel();
                if (_monitorTask != null)
                {
                    await Task.WhenAny(_monitorTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
                }
                _runCts.Dispose();
                _runCts = null;
            }
            
            KillProcessIfRunning();
            
            _status = "stopped";
            _healthRegistry.Unregister(this);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetQuarantineAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _recentCrashes.Clear();
            _crashCount = 0;
            _status = "stopped";
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task RunMonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_status == "quarantined" || _status == "failed")
            {
                break;
            }

            try
            {
                bool startSuccess = await TryStartProcessAsync(ct);
                if (!startSuccess)
                {
                    await HandleCrashAsync(ct);
                    continue;
                }

                _status = "healthy";

                await _process!.WaitForExitAsync(ct);
                
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                if (_process.ExitCode == 0)
                {
                    // Clean exit, do not trigger restart policy
                    await _lock.WaitAsync(ct);
                    try
                    {
                        _status = "stopped";
                    }
                    finally
                    {
                        _lock.Release();
                    }
                    break;
                }

                await HandleCrashAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker monitor loop.");
                await HandleCrashAsync(ct);
            }
        }
    }

    private async Task<bool> TryStartProcessAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            KillProcessIfRunning();

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            
            foreach (var kvp in _options.EnvironmentVariables)
            {
                _process.StartInfo.Environment[kvp.Key] = kvp.Value;
            }

            return _process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process {ExecutablePath}", _executablePath);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task HandleCrashAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _crashCount++;
            var now = _clock.UtcNow;
            
            _recentCrashes.Add(now);
            _recentCrashes.RemoveAll(t => now - t > _options.CrashLoopWindow);

            if (_recentCrashes.Count >= _options.CrashLoopThreshold)
            {
                _logger.LogWarning("Worker {Component} crashed {Count} times within {Window} — quarantining.", Component, _recentCrashes.Count, _options.CrashLoopWindow);
                _status = "quarantined";
                return;
            }

            if (_crashCount > 3)
            {
                _status = "failed";
                return;
            }

            _status = "degraded";
            
            TimeSpan delay = _crashCount switch
            {
                1 => _options.FirstRestartDelay,
                2 => _options.SecondRestartDelay,
                3 => _options.ThirdRestartDelay,
                _ => TimeSpan.Zero
            };

            await _delayProvider.DelayAsync(delay, ct);
            
            _status = "starting";
        }
        finally
        {
            _lock.Release();
        }
    }

    private void KillProcessIfRunning()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch
            {
                // Ignore exceptions during kill
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await StopAsync(CancellationToken.None);
        _lock.Dispose();
    }
}
