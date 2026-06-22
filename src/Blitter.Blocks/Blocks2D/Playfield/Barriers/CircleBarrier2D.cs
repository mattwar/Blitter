using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// A circular barrier — pinball bumpers, posts, round corners.
/// </summary>
public class CircleBarrier2D : Barrier2D
{
    public Vector2 Center { get; }
    public float Radius { get; }

    public CircleBarrier2D(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius < 0f ? 0f : radius;
    }

    public CircleBarrier2D(float x, float y, float radius)
        : this(new Vector2(x, y), radius) { }

    public override PosedHitShape2D HitShape =>
        new(new CircleHitShape2D(Vector2.Zero, Radius),
            new Pose2D(Center, 0f, 1f));
}
