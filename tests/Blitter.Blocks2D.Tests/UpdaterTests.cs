namespace Blitter.Blocks2D.Tests;

public class UpdaterTests
{
    [Fact]
    public void Update_TicksEntityBehaviorAndChildren()
    {
        var root = new TestContainer();
        var child = new TestEntity();
        var childBehavior = child.GetOrAddBehavior<TestBehavior>();
        root.AddEntity(child);

        Updater.Default.Update(root, new EntityUpdateContext());

        Assert.Equal(1, child.Updates);
        Assert.Equal(1, childBehavior.Updates);
    }

    [Fact]
    public void Update_DoesNotWalkChildrenWhenContainerIsUpdatable()
    {
        var root = new TestUpdatableContainer();
        var child = new TestEntity();
        root.AddEntity(child);

        Updater.Default.Update(root, new EntityUpdateContext());

        Assert.Equal(1, root.Updates);
        Assert.Equal(0, child.Updates);
    }

    [Fact]
    public void Update_CollisionSpaceUsesCollisionSubsteps()
    {
        var root = new TestCollisionSpaceContainer { Substeps = 3 };
        var child = new TestEntity();
        root.AddEntity(child);

        Updater.Default.Update(root, new EntityUpdateContext());

        Assert.Equal(3, child.Updates);
    }

    private class TestEntity : Entity, IUpdatable
    {
        public int Updates { get; private set; }

        public void Update(in EntityUpdateContext context)
        {
            Updates++;
        }
    }

    private class TestContainer : Entity, IContainer
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

    private sealed class TestUpdatableContainer : TestContainer, IUpdatable
    {
        public int Updates { get; private set; }

        public void Update(in EntityUpdateContext context)
        {
            Updates++;
        }
    }

    private sealed class TestCollisionSpaceContainer : TestContainer, ICollisionSpace
    {
        public int Substeps { get; init; } = 1;

        public int GetCollisionSubstepCount(in EntityUpdateContext context) => Substeps;

        public void ResolveCollisions() { }
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