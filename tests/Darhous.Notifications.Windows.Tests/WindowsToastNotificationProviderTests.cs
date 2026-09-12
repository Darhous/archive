using System;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Modules.Notifications;
using Xunit;

namespace Darhous.Notifications.Windows.Tests;

public class WindowsToastNotificationProviderTests
{
    [Fact]
    public async Task ShowAsync_ShouldCallToastApiWithoutThrowing()
    {
        var provider = new WindowsToastNotificationProvider();
        
        var notification = new Notification(
            Id: 1,
            Uid: Guid.NewGuid(),
            UserId: null,
            Type: "test",
            Severity: "info",
            Title: "Test Title",
            Body: "Test Body",
            Source: "test",
            ActionJson: "{\"some\":\"action\"}",
            CreatedAt: DateTimeOffset.UtcNow,
            ReadAt: null,
            DismissedAt: null
        );

        // We can't easily mock the static ToastContentBuilder API,
        // but we can verify it doesn't crash during construction/show.
        // It might not render visually in a headless session.
        await provider.ShowAsync(notification, CancellationToken.None);
        
        // Assert: no exception was thrown
    }
}
