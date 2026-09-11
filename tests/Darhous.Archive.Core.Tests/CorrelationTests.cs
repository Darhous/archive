using Darhous.Archive.Core.Correlation;

namespace Darhous.Archive.Core.Tests;

public class CorrelationTests
{
    [Fact]
    public void New_ProducesDistinctIds()
    {
        Assert.NotEqual(CorrelationId.New(), CorrelationId.New());
    }

    [Fact]
    public async Task Accessor_FlowsAcrossAwait_ButNotAcrossParallelBranches()
    {
        var accessor = new AsyncLocalCorrelationContextAccessor();
        var id = CorrelationId.New();
        accessor.Current = id;

        await Task.Yield();

        Assert.Equal(id, accessor.Current);
    }
}
