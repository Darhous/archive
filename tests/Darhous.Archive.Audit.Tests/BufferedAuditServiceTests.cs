using Darhous.Archive.Contracts.Audit;

namespace Darhous.Archive.Audit.Tests;

public class BufferedAuditServiceTests : AuditTestBase
{
    [Fact]
    public async Task RecordAsync_CriticalAction_IsWrittenImmediately()
    {
        Assert.Contains(AuditAction.Login, AuditAction.CriticalActions);

        await AuditService.RecordAsync(
            new AuditEntry(AuditAction.Login, AuditActionCategory.Session, AuditResult.Success, UsernameSnapshot: "ahmed"),
            CancellationToken.None);

        // No wait needed — RecordAsync for a critical action doesn't return until committed.
        var count = await CountAuditEventsAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RecordAsync_NonCriticalAction_IsEventuallyFlushed()
    {
        Assert.DoesNotContain(AuditAction.Search, AuditAction.CriticalActions);

        await AuditService.RecordAsync(
            new AuditEntry(AuditAction.Search, AuditActionCategory.Search, AuditResult.Success, SearchQuery: "الحماية المدنية"),
            CancellationToken.None);

        // RecordAsync returns as soon as it's buffered, not once it's on disk — poll briefly
        // for the periodic flush (every 2s) to catch up, well under its own timeout budget.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        long count;
        do
        {
            count = await CountAuditEventsAsync();
            if (count > 0) break;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task QueryAsync_FiltersByAction()
    {
        await AuditService.RecordAsync(new AuditEntry(AuditAction.Login, AuditActionCategory.Session, AuditResult.Success), CancellationToken.None);
        await AuditService.RecordAsync(new AuditEntry(AuditAction.FailedLogin, AuditActionCategory.Session, AuditResult.Failure), CancellationToken.None);

        var results = await AuditService.QueryAsync(new AuditQuery(Action: AuditAction.Login), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(AuditAction.Login, results[0].Action);
    }

    [Fact]
    public async Task QueryAsync_OrdersNewestFirst()
    {
        await AuditService.RecordAsync(new AuditEntry(AuditAction.Login, AuditActionCategory.Session, AuditResult.Success, UsernameSnapshot: "first"), CancellationToken.None);
        await Task.Delay(10);
        await AuditService.RecordAsync(new AuditEntry(AuditAction.Login, AuditActionCategory.Session, AuditResult.Success, UsernameSnapshot: "second"), CancellationToken.None);

        var results = await AuditService.QueryAsync(new AuditQuery(), CancellationToken.None);

        Assert.Equal("second", results[0].UsernameSnapshot);
        Assert.Equal("first", results[1].UsernameSnapshot);
    }

    [Fact]
    public async Task RecordAsync_FailedLogin_PreservesErrorCode()
    {
        await AuditService.RecordAsync(
            new AuditEntry(AuditAction.FailedLogin, AuditActionCategory.Session, AuditResult.Failure, ErrorCode: "AUTH_INVALID_CREDENTIALS"),
            CancellationToken.None);

        var results = await AuditService.QueryAsync(new AuditQuery(Action: AuditAction.FailedLogin), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(AuditResult.Failure, results[0].Result);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", results[0].ErrorCode);
    }
}
