using Darhous.Archive.Core.Time;

namespace Darhous.Archive.Core.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_IsCloseToRealTime()
    {
        var clock = new SystemClock();

        var delta = DateTimeOffset.UtcNow - clock.UtcNow;

        Assert.True(Math.Abs(delta.TotalSeconds) < 5);
    }
}
