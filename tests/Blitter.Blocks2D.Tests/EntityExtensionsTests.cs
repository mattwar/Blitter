namespace Blitter.Blocks2D.Tests;

public class EntityExtensionsTests
{
    [Fact]
    public void TryGetCapability_ReturnsEntityWhenEntityImplementsCapability()
    {
        var entity = new CapabilityEntity();

        var found = entity.TryGetCapability<ITestCapability>(out var capability);

        Assert.True(found);
        Assert.Same(entity, capability);
    }

    [Fact]
    public void TryGetCapability_ReturnsBehaviorWhenBehaviorImplementsCapability()
    {
        var entity = new Entity
        {
            Behaviors = [new CapabilityBehavior()]
        };

        var found = entity.TryGetCapability<ITestCapability>(out var capability);

        Assert.True(found);
        Assert.IsType<CapabilityBehavior>(capability);
    }

    [Fact]
    public void TryGetBehavior_DoesNotReturnEntityBehaviorSubtype()
    {
        var entity = new CapabilityEntity();

        var found = entity.TryGetBehavior<CapabilityBehavior>(out var behavior);

        Assert.False(found);
        Assert.Null(behavior);
    }

    [Fact]
    public void GetCapability_ThrowsWhenCapabilityIsMissing()
    {
        var entity = new Entity();

        Assert.Throws<InvalidOperationException>(() => entity.GetCapability<ITestCapability>());
    }

    private interface ITestCapability
    {
    }

    private sealed class CapabilityEntity : Entity, ITestCapability
    {
    }

    private sealed class CapabilityBehavior : Behavior, ITestCapability
    {
    }
}
