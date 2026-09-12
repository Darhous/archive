using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;

var pipeName = Environment.GetEnvironmentVariable("SCANNER_PIPE_NAME");
var sessionToken = Environment.GetEnvironmentVariable("SCANNER_SESSION_TOKEN");

if (string.IsNullOrEmpty(pipeName) || string.IsNullOrEmpty(sessionToken))
{
    Console.WriteLine("Missing env vars");
    return -1;
}

var protocolOptions = new WorkerProtocolOptions();
var transport = await NamedPipeTransport.ConnectClientAsync(pipeName, protocolOptions);

var client = new WorkerHandshakeClient(protocolOptions);
var desc = new WorkerHandshakeDescription("test-worker", "1.0", new[] { "scan" }, new WorkerHealthPayload("healthy"));
await client.PerformAsync(transport, desc, sessionToken);

while (true)
{
    var msg = await transport.ReceiveAsync(CancellationToken.None);
    if (msg.MessageType == "scan.request")
    {
        var req = msg.DeserializePayload<System.Text.Json.JsonElement>();
        var shouldFail = req.TryGetProperty("Resolution", out var resToken) && resToken.GetInt32() == -1;

        if (shouldFail)
        {
            var failure = new { Code = "ScanError", Message = "Hardware failure simulated" };
            await transport.SendAsync(WorkerMessage.Create(protocolOptions.ProtocolVersion, "scan.result", failure));
        }
        else
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "dummy");
            
            var res = new LargeDataReference(tempFile);
            await transport.SendAsync(WorkerMessage.Create(protocolOptions.ProtocolVersion, "scan.result", res));
        }
    }
}
