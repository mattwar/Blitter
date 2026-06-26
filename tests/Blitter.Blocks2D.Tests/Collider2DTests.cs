using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks2D.Tests;

public class Collider2DTests
{
    [Fact]
    public void Collide_RootEntity_CollidesEachCollisionSpaceIndependently()
    {
        var firstA = new TestCollider(new Vector2(0, 0));
        var firstB = new TestCollider(new Vector2(0, 0));
        var secondA = new TestCollider(new Vector2(0, 0));
        var secondB = new TestCollider(new Vector2(0, 0));
        var root = new Container
        {
            Entities =
            [
                new PlayField2D { Entities = [firstA, firstB] },
                new PlayField2D { Entities = [secondA, secondB] },
            ]
        };

        new Collider2D().Collide(root);

        Assert.Equal(1, firstA.Hits);
        Assert.Equal(1, firstB.Hits);
        Assert.Equal(1, secondA.Hits);
        Assert.Equal(1, secondB.Hits);
    }

    [Fact]
    public void Collide_RootEntity_DoesNotCollideAcrossSiblingCollisionSpaces()
    {
        var first = new TestCollider(new Vector2(0, 0));
        var second = new TestCollider(new Vector2(0, 0));
        var root = new Container
        {
            Entities =
            [
                new PlayField2D { Entities = [first] },
                new PlayField2D { Entities = [second] },
            ]
        };

        new Collider2D().Collide(root);

        Assert.Equal(0, first.Hits);
        Assert.Equal(0, second.Hits);
    }

    [Fact]
    public void Collide_RootEntity_UsesCollisionSpaceBehavior()
    {
        var first = new TestCollider(new Vector2(0, 0));
        var second = new TestCollider(new Vector2(0, 0));
        var root = new Container
        {
            Entities =
            [
                new Container
                {
                    Entities = [first, second],
                    Behaviors = [new CollisionSpace2D()],
                }
            ]
        };

        new Collider2D().Collide(root);

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
