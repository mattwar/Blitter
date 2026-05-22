using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks;

/// <summary>
/// Default single-circle shape derived from a sprite's image
/// boundary scaled by <see cref="Sprite2D.Scale"/>. Allocated once
/// per sprite; reads live position each dispatch so it tracks the
/// sprite automatically.
/// </summary>
public sealed class CircleHitShape : HitShape
{
    private readonly Sprite2D _sprite;

    /// <summary>
    /// Creates a circle shape that tracks <paramref name="sprite"/>'s image boundary.
    /// </summary>
    public CircleHitShape(Sprite2D sprite) => _sprite = sprite;

    /// <inheritdoc/>
    public override BoundingCircle BroadCircle => ComputeCircle();

    /// <inheritdoc/>
    public override bool TestHit(HitShape other, Hitter hitter)
    {
        Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
        var c = ComputeCircle();
        mine[0] = HitPrimitive.Circle(c.Center, c.Radius);
        return hitter.TestHit(mine, other);
    }

    /// <inheritdoc/>
    public override bool TestHitWith(ReadOnlySpan<HitPrimitive> other, Hitter hitter)
    {
        Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
        var c = ComputeCircle();
        mine[0] = HitPrimitive.Circle(c.Center, c.Radius);
        return hitter.TestHit(other, mine);
    }

    /// <inheritdoc/>
    public override void Visit(HitShapeVisitor visitor)
    {
        Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
        var c = ComputeCircle();
        mine[0] = HitPrimitive.Circle(c.Center, c.Radius);
        visitor(mine);
    }

    private BoundingCircle ComputeCircle()
    {
        // Sprites without an Image (e.g., raw TextSprite2D) collapse to
        // a zero-radius circle, which PlayField2D treats as "uncollidable".
        if (_sprite.Image is null)
            return new BoundingCircle(_sprite.Center, 0f);
        var b = _sprite.Image.Boundary;
        if (b.IsEmpty)
            return new BoundingCircle(_sprite.Center, 0f);
        // Boundary center is in unrotated, unscaled sprite-local
        // space. Rotate by the sprite's orientation so the circle
        // tracks off-center art as the sprite spins.
        var radians = _sprite.Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var local = b.Center * _sprite.Scale;
        var offset = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
        return new BoundingCircle(_sprite.Center + offset, b.Radius * _sprite.Scale);
    }
}

/// <summary>
/// A single-capsule shape oriented by the sprite's
/// <see cref="Sprite2D.Rotation"/> and scaled by
/// <see cref="Sprite2D.Scale"/>. The two endpoints are supplied in
/// the sprite's local frame (origin = sprite center, +Y down,
/// 0° rotation pointing up — matches <see cref="Sprite2D.Heading"/>).
/// </summary>
/// <remarks>
/// Common usage is an elongated body (rocket, missile, sword): pass
/// the two endpoints of the body's center line and the body's half-width.
/// </remarks>
public sealed class CapsuleHitShape : HitShape
{
    private readonly Sprite2D _sprite;
    private readonly Vector2 _localA;
    private readonly Vector2 _localB;
    private readonly float _radius;

    /// <summary>
    /// Creates a capsule shape on <paramref name="sprite"/> with the
    /// given local-space endpoints and radius. Endpoints are in the
    /// sprite's unscaled, unrotated local frame.
    /// </summary>
    public CapsuleHitShape(Sprite2D sprite, Vector2 localEndA, Vector2 localEndB, float radius)
    {
        _sprite = sprite;
        _localA = localEndA;
        _localB = localEndB;
        _radius = radius;
    }

    /// <inheritdoc/>
    public override BoundingCircle BroadCircle
    {
        get
        {
            ComputeCapsule(out var a, out var b, out var r);
            var center = (a + b) * 0.5f;
            var half = (a - b).Length() * 0.5f;
            return new BoundingCircle(center, half + r);
        }
    }

    /// <inheritdoc/>
    public override bool TestHit(HitShape other, Hitter hitter)
    {
        Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
        ComputeCapsule(out var a, out var b, out var r);
        mine[0] = HitPrimitive.Capsule(a, b, r);
        return hitter.TestHit(mine, other);
    }

    /// <inheritdoc/>
    public override bool TestHitWith(ReadOnlySpan<HitPrimitive> other, Hitter hitter)
    {
        Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
        ComputeCapsule(out var a, out var b, out var r);
        mine[0] = HitPrimitive.Capsule(a, b, r);
        return hitter.TestHit(other, mine);
    }

    /// <inheritdoc/>
    public override void Visit(HitShapeVisitor visitor)
    {
        Span<HitPrimitive> mine = stackalloc HitPrimitive[1];
        ComputeCapsule(out var a, out var b, out var r);
        mine[0] = HitPrimitive.Capsule(a, b, r);
        visitor(mine);
    }

    private void ComputeCapsule(out Vector2 worldA, out Vector2 worldB, out float worldRadius)
    {
        // Sprite rotation (degrees, 0 = up) maps local (0,-1) to the
        // sprite's forward direction (sin θ, -cos θ). The same matrix
        // applied to any local point yields its world-space offset.
        var radians = _sprite.Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var scale = _sprite.Scale;
        var center = _sprite.Center;
        worldA = center + Rotate(_localA, cos, sin) * scale;
        worldB = center + Rotate(_localB, cos, sin) * scale;
        worldRadius = _radius * scale;
    }

    private static Vector2 Rotate(Vector2 v, float cos, float sin) =>
        new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
}
