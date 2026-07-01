namespace Blitter.Blocks2D.Tests;

public class ContainerTests
{
    [Fact]
    public void AddEntity_AttachesChild()
    {
        var container = new Container();
        var child = new Entity();

        container.AddEntity(child);

        Assert.Same(container, child.Container);
        Assert.Same(child, Assert.Single(container.Entities));
    }

    [Fact]
    public void RemoveEntity_DetachesChild()
    {
        var container = new Container();
        var child = new Entity();
        container.AddEntity(child);

        container.RemoveEntity(child);

        Assert.Null(child.Container);
        Assert.Empty(container.Entities);
    }

    [Fact]
    public void Entities_Init_AttachesChildren()
    {
        var child = new Entity();

        var container = new Container { Entities = [child] };

        Assert.Same(container, child.Container);
        Assert.Same(child, Assert.Single(container.Entities));
    }

    [Fact]
    public void AddEntity_ReparentsChild()
    {
        var first = new Container();
        var second = new Container();
        var child = new Entity();
        first.AddEntity(child);

        second.AddEntity(child);

        Assert.Empty(first.Entities);
        Assert.Same(second, child.Container);
        Assert.Same(child, Assert.Single(second.Entities));
    }
}
