using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// A circular barrier — pinball bumpers, posts, round corners.
/// Wires up a <see cref="CollisionShape2D"/> circle posed by its
/// <see cref="Barrier2D.Transform"/>.
/// </summary>
public class CircleBarrier2D : Barrier2D
{
    public CircleBarrier2D(Vector2 center, float radius)
    {
        Transform.Position = center;
        this.GetOrAddTrait<CollisionShape2D>().Shape =
            new CircleHitShape2D(Vector2.Zero, radius < 0f ? 0f : radius);
    }

    public CircleBarrier2D(float x, float y, float radius)
        : this(new Vector2(x, y), radius) { }

    /// <summary>World-space center of the circle.</summary>
    public Vector2 Center
    {
        get => Transform.Position;
        set => Transform.Position = value;
    }

    /// <summary>Radius in world units. Never negative.</summary>
    public float Radius
    {
        get => this.GetOrAddTrait<CollisionShape2D>().Shape is CircleHitShape2D circle
            ? circle.LocalRadius
            : 0f;
        set => this.GetOrAddTrait<CollisionShape2D>().Shape =
            new CircleHitShape2D(Vector2.Zero, value < 0f ? 0f : value);
    }
}
