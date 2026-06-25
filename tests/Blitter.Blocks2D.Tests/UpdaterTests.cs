namespace Blitter.Blocks2D.Tests;

public class UpdaterTests
{
    [Fact]
    public void Update_TicksEntityBehaviorAndChildren()
    {
        var root = new TestContainer();
        var child = new TestEntity();
        var rootBehavior = root.GetOrAddBehavior<TestBehavior>();
        var childBehavior = child.GetOrAddBehavior<TestBehavior>();
        root.AddEntity(child);

        Updater.Default.Update(root, new EntityUpdateContext());

        Assert.Equal(1, root.Updates);
        Assert.Equal(1, rootBehavior.Updates);
        Assert.Equal(1, child.Updates);
        Assert.Equal(1, childBehavior.Updates);
    }

    [Fact]
    public void UpdateEntity_DoesNotWalkChildren()
    {
        var root = new TestContainer();
        var child = new TestEntity();
        root.AddEntity(child);

        Updater.Default.UpdateEntity(root, new EntityUpdateContext());

        Assert.Equal(1, root.Updates);
        Assert.Equal(0, child.Updates);
    }

    [Fact]
    public void Update_DoesNotWalkChildrenWhenContainerOwnsTraversal()
    {
        var root = new TraversalOwnerContainer();
        var child = new TestEntity();
        root.AddEntity(child);

        Updater.Default.Update(root, new EntityUpdateContext());

        Assert.Equal(1, root.Updates);
        Assert.Equal(0, child.Updates);
    }

    private class TestEntity : Entity, IUpdatable
    {
        public int Updates { get; private set; }

        public void Update(in EntityUpdateContext context)
        {
            Updates++;
        }
    }

    private class TestContainer : TestEntity, IContainerEntity
    {
        private readonly List<IEntity> _entities = new();

        public IReadOnlyList<IEntity> Entities => _entities;

        public void AddEntity(IEntity child)
        {
            _entities.Add(child);
            if (child is Entity entity)
                entity.Container = this;
        }

        public void RemoveEntity(IEntity child)
        {
            if (_entities.Remove(child) && child is Entity entity && entity.Container == this)
                entity.Container = null;
        }
    }

    private sealed class TraversalOwnerContainer : TestContainer, IUpdateTraversalOwner
    {
    }

    private sealed class TestBehavior : Behavior, IUpdatable
    {
        public int Updates { get; private set; }

        public void Update(in EntityUpdateContext context)
        {
            Updates++;
        }
    }
}