using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks2D.Tests;

public class Collider2DTests
{
    [Fact]
    public void ResolveCollisions_CollidesSpaceMembers()
    {
        var first = new TestCollider(new Vector2(0, 0));
        var second = new TestCollider(new Vector2(0, 0));
        var container = new Container
        {
            Entities = [first, second],
            Behaviors = [new CollisionSpace2D()],
        };

        Assert.True(container.TryGetCapability<ICollisionSpace>(out var space));
        space.ResolveCollisions();

        Assert.Equal(1, first.Hits);
        Assert.Equal(1, second.Hits);
    }

    private sealed class TestCollider : Entity, IColliderShape2D
    {
        private readonly PosedHitShape2D _shape;

        public int Hits { get; private set; }

        public TestCollider(Vector2 center)
        {
            _shape = new PosedHitShape2D(new CircleHitShape2D(Vector2.Zero, 10f), new Pose2D(center, 0f, 1f));
            Behaviors = [new HitCounter(this)];
        }

        public PosedHitShape2D GetShape() => _shape;

        private sealed class HitCounter(TestCollider entity) : Behavior, IHittable2D
        {
            public void OnHit(in Hit2D hit) => entity.Hits++;
        }
    }
}
