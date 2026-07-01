using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks2D.Tests;

public class PlayField2DTests
{
    private sealed class RemoveSelfOnUpdate : Behavior, IUpdatable
    {
        public Containment ContainmentDuringUpdate { get; private set; }

        public void Update(in EntityUpdateContext context)
        {
            var playfield = (PlayField2D)Entity!.Container!;
            playfield.RemoveEntity(Entity!);
            ContainmentDuringUpdate = playfield.GetContainment(Entity!);
        }
    }

    [Fact]
    public void AddEntity_AcceptsPlainEntity()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();

        playfield.AddEntity(entity);

        Assert.Same(entity, Assert.Single(playfield.Entities));
        Assert.Same(playfield, entity.Container);
        Assert.True(playfield.TryGetEntity<Entity>(out var found));
        Assert.Same(entity, found);
        Assert.Equal(Containment.Contained, playfield.GetContainment(entity));

        playfield.RemoveEntity(entity);

        Assert.Empty(playfield.Entities);
        Assert.Null(entity.Container);
        Assert.Equal(Containment.NotContained, playfield.GetContainment(entity));
    }

    [Fact]
    public void Barrier2D_IsColliderBarrier()
    {
        var barrier = new LineBarrier2D(Vector2.Zero, Vector2.One);

        Assert.IsAssignableFrom<IColliderBarrier2D>(barrier);
    }

    [Fact]
    public void AddEntity_DoesNotDuplicateExistingEntity()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();

        playfield.AddEntity(entity);
        playfield.AddEntity(entity);

        Assert.Same(entity, Assert.Single(playfield.Entities));
        Assert.Same(playfield, entity.Container);
    }

    [Fact]
    public void RemoveEntity_RemovesAnyContainedEntity()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();
        var barrier = new LineBarrier2D(Vector2.Zero, Vector2.One);

        playfield.AddEntity(entity);
        playfield.AddEntity(barrier);

        playfield.RemoveEntity(entity);
        playfield.RemoveEntity(barrier);

        Assert.Empty(playfield.Entities);
        Assert.Null(entity.Container);
        Assert.Null(barrier.Container);
    }

    [Fact]
    public void RemoveEntity_DuringUpdate_ReportsRemovingUntilFrameEnd()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();
        var removeSelf = entity.GetOrAddBehavior<RemoveSelfOnUpdate>();
        playfield.AddEntity(entity);

        Updater.Default.Update(playfield, new EntityUpdateContext());

        Assert.Equal(Containment.Removing, removeSelf.ContainmentDuringUpdate);
        Assert.Empty(playfield.Entities);
        Assert.Null(entity.Container);
    }

    [Fact]
    public void CollisionSpaceBehavior_ComputesSubstepsForPlayfield()
    {
        var playfield = new PlayField2D();
        var entity = new TestCollider();
        entity.GetOrAddTrait<Velocity2D>().Speed = 100f;
        playfield.AddEntity(entity);

        Assert.True(playfield.TryGetCapability<ICollisionSpace>(out var collisionSpace));
        Assert.Equal(8, collisionSpace.GetCollisionSubstepCount(new EntityUpdateContext
        {
            ElapsedSinceLastUpdate = TimeSpan.FromSeconds(1),
        }));
    }

    private sealed class TestCollider : Entity, IColliderShape2D
    {
        private readonly PosedHitShape2D _shape = new(new CircleHitShape2D(Vector2.Zero, 10f), Pose2D.Identity);

        public PosedHitShape2D GetShape() => _shape;
    }
}