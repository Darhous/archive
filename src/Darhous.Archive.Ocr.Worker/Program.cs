using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Ocr.Worker;

public static class Program
{
    public const string PipeVariable = "DARHOUS_WORKER_PIPE_NAME";
    public const string TokenVariable = "DARHOUS_WORKER_SESSION_TOKEN";
    public const string TempVariable = "DARHOUS_WORKER_TEMP_ROOT";

    public static async Task<int> Main()
    {
        var pipeName = Environment.GetEnvironmentVariable(PipeVariable);
        var token = Environment.GetEnvironmentVariable(TokenVariable);
        Environment.SetEnvironmentVariable(TokenVariable, null);
        if (string.IsNullOrWhiteSpace(pipeName) || string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine($"Set {PipeVariable} and {TokenVariable} in the launch environment.");
            return 2;
        }

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            var defaults = new WorkerProtocolOptions();
            var options = new WorkerProtocolOptions
            {
                ManagedTempStorageRoot = Environment.GetEnvironmentVariable(TempVariable) ?? defaults.ManagedTempStorageRoot,
            };
            using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            connectionTimeout.CancelAfter(options.HandshakeTimeout);
            await using var transport = await NamedPipeTransport.ConnectClientAsync(
                pipeName,
                options,
                connectionTimeout.Token);
            using var engine = new TesseractOcrEngine();
            await new OcrWorkerSession(engine, options).RunAsync(transport, token, shutdown.Token);
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("OCR worker startup or transport failed.");
            return 1;
        }
    }
}
