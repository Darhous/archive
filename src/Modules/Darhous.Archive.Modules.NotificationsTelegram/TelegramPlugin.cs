using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Contracts.Events;
using Darhous.Archive.PluginSdk;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.Archive.Modules.NotificationsTelegram;

public sealed class TelegramPlugin : IArchivePlugin
{
    public PluginIdentity Identity => new("Darhous.Notifications.Telegram", new Version(1, 0, 0), "Darhous", "Telegram Notifications");
    
    // In a real app we might inject an HttpClient or use a factory, but we'll use a static client for now.
    public static HttpClient HttpClient { get; set; } = new HttpClient();

    private IPluginRuntimeContext? _runtimeContext;

    public ValueTask ConfigureAsync(IPluginConfigurationContext context, CancellationToken cancellationToken)
    {
        context.Events.Subscribe<JobFailedEvent>(async (envelope, ct) =>
        {
            if (_runtimeContext == null) return;
            
            var payload = envelope.Payload;
            
            // Filter to severity == "critical" or "error"
            if (payload.Severity != "critical" && payload.Severity != "error")
                return;

            string botToken = await _runtimeContext.Secrets.GetAsync("telegram_bot_token", ct) ?? "fake-token";
            string chatId = await _runtimeContext.Secrets.GetAsync("telegram_chat_id", ct) ?? "fake-chat-id";

            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            
            var requestBody = new
            {
                chat_id = chatId,
                text = $"Job Failed: {payload.JobType}\nError: {payload.ErrorMessage}"
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            
            var response = await HttpClient.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
        });

        return ValueTask.CompletedTask;
    }

    public ValueTask StartAsync(IPluginRuntimeContext context, CancellationToken cancellationToken)
    {
        _runtimeContext = context;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        _runtimeContext = null;
        return ValueTask.CompletedTask;
    }
}
