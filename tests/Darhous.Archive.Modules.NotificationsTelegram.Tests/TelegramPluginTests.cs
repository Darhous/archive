using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Contracts.Events;
using Darhous.Archive.PluginSdk;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;
using Moq;
using Moq.Protected;
using Xunit;

namespace Darhous.Archive.Modules.NotificationsTelegram.Tests;

public class TelegramPluginTests
{
    [Fact]
    public async Task ConfigureAsync_ShouldSendHttpRequestOnCriticalEvent()
    {
        var plugin = new TelegramPlugin();

        var eventsMock = new Mock<IEventSubscriptionRegistry>();
        Func<ArchiveEventEnvelope<JobFailedEvent>, CancellationToken, Task>? capturedHandler = null;

        eventsMock
            .Setup(x => x.Subscribe<JobFailedEvent>(It.IsAny<Func<ArchiveEventEnvelope<JobFailedEvent>, CancellationToken, Task>>()))
            .Callback<Func<ArchiveEventEnvelope<JobFailedEvent>, CancellationToken, Task>>(handler => capturedHandler = handler);

        var configContextMock = new Mock<IPluginConfigurationContext>();
        configContextMock.Setup(x => x.Events).Returns(eventsMock.Object);

        await plugin.ConfigureAsync(configContextMock.Object, CancellationToken.None);

        Assert.NotNull(capturedHandler);

        var secretsMock = new Mock<IPluginSecrets>();
        secretsMock.Setup(x => x.GetAsync("telegram_bot_token", It.IsAny<CancellationToken>())).ReturnsAsync("fake-token");
        secretsMock.Setup(x => x.GetAsync("telegram_chat_id", It.IsAny<CancellationToken>())).ReturnsAsync("fake-chat-id");

        var runtimeContextMock = new Mock<IPluginRuntimeContext>();
        runtimeContextMock.Setup(x => x.Secrets).Returns(secretsMock.Object);

        await plugin.StartAsync(runtimeContextMock.Object, CancellationToken.None);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        TelegramPlugin.HttpClient = new HttpClient(handlerMock.Object);

        var envelope = new ArchiveEventEnvelope<JobFailedEvent>(
            Guid.NewGuid(), 
            DateTimeOffset.UtcNow, 
            "job.failed", 
            new JobFailedEvent("test-job", Guid.NewGuid(), "error message", "critical"), 
            null, 
            null);

        await capturedHandler(envelope, CancellationToken.None);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Post && 
                req.RequestUri!.ToString().Contains("api.telegram.org/botfake-token/sendMessage")),
            ItExpr.IsAny<CancellationToken>());
    }
}
