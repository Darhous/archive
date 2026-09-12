using Darhous.Archive.Workers.Protocol;

namespace Darhous.Archive.Workers.Tests;

public sealed class ManagedTempPathValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_AcceptsDescendantOfManagedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var validator = CreateValidator(root);
        var candidate = Path.Combine(root, "worker-1", "payload.pdf");

        var result = validator.ValidateAndNormalize(candidate);

        Assert.Equal(Path.GetFullPath(candidate), result);
    }

    [Fact]
    public void IsWithinManagedTempRoot_RejectsTraversalOutsideRoot()
    {
        var parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "managed");
        var validator = CreateValidator(root);
        var candidate = Path.Combine(root, "..", "outside.pdf");

        Assert.False(validator.IsWithinManagedTempRoot(candidate));
    }

    [Fact]
    public void IsWithinManagedTempRoot_RejectsSiblingWithSameTextPrefix()
    {
        var parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "managed");
        var validator = CreateValidator(root);
        var candidate = Path.Combine(parent, "managed-attacker", "payload.pdf");

        Assert.False(validator.IsWithinManagedTempRoot(candidate));
    }

    [Fact]
    public void IsWithinManagedTempRoot_RejectsRootItselfBecauseItIsNotADataFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var validator = CreateValidator(root);

        Assert.False(validator.IsWithinManagedTempRoot(root));
    }

    [Fact]
    public void LargeDataReference_HasDedicatedProtocolMessageType()
    {
        var reference = new LargeDataReference("payload.bin");
        var message = WorkerMessage.Create(1, WorkerMessageTypes.LargeDataReference, reference);

        Assert.Equal("data.path-reference", message.MessageType);
        Assert.Equal(reference, message.DeserializePayload<LargeDataReference>());
    }

    private static ManagedTempPathValidator CreateValidator(string root) =>
        new(new WorkerProtocolOptions { ManagedTempStorageRoot = root });
}
