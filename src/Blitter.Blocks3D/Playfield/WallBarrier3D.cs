using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A finite oriented rectangular wall — floor tile, ceiling, room wall,
/// platform face. The 3D analog of <c>Blitter.Blocks2D.LineBarrier2D</c>:
/// has a winding-dependent <see cref="Normal"/> side and an optional
/// <see cref="OneSided"/> flag for one-way surfaces.
/// </summary>
public class WallBarrier3D : Barrier3D
{
    /// <summary>World-space center of the rectangle.</summary>
    public Vector3 Center { get; }

    /// <summary>Unit-length surface normal. Defines the "front" side.</summary>
    public Vector3 Normal { get; }

    /// <summary>
    /// Unit-length in-plane axis along the rectangle's width
    /// (<see cref="HalfExtents"/>.X). Always perpendicular to
    /// <see cref="Normal"/>.
    /// </summary>
    public Vector3 Tangent { get; }

    /// <summary>
    /// Unit-length in-plane axis along the rectangle's height
    /// (<see cref="HalfExtents"/>.Y); equals
    /// <c>Cross(Normal, Tangent)</c>. Cached so the intersection test
    /// doesn't recompute it.
    /// </summary>
    public Vector3 Bitangent { get; }

    /// <summary>Half-width along <see cref="Tangent"/>, half-height along <see cref="Bitangent"/>.</summary>
    public Vector2 HalfExtents { get; }

    /// <summary>
    /// When true, sprites only collide when their center lies on the
    /// <see cref="Normal"/> side of the rectangle's supporting plane.
    /// </summary>
    public bool OneSided { get; set; }

    /// <summary>
    /// Builds a wall centered at <paramref name="center"/> facing
    /// <paramref name="normal"/>, with the rectangle's "width" axis
    /// along <paramref name="tangentHint"/> (projected to be
    /// perpendicular to the normal) and the given half-extents.
    /// </summary>
    public WallBarrier3D(Vector3 center, Vector3 normal, Vector3 tangentHint, Vector2 halfExtents)
    {
        Center = center;

        var n = normal.LengthSquared() > float.Epsilon
            ? Vector3.Normalize(normal)
            : Vector3.UnitY;
        Normal = n;

        // Remove the component of tangentHint that is parallel to the
        // normal so Tangent is guaranteed to lie in the wall's plane.
        var t = tangentHint - n * Vector3.Dot(n, tangentHint);
        if (t.LengthSquared() <= float.Epsilon)
            t = PickPerpendicular(n);
        t = Vector3.Normalize(t);
        Tangent = t;

        Bitangent = Vector3.Cross(n, t);
        HalfExtents = new Vector2(
            halfExtents.X < 0f ? 0f : halfExtents.X,
            halfExtents.Y < 0f ? 0f : halfExtents.Y);
    }

    // Any axis not parallel to n, then orthogonalised. Picks the world
    // axis least aligned with n so we don't end up degenerate.
    private static Vector3 PickPerpendicular(Vector3 n)
    {
        var ax = MathF.Abs(n.X);
        var ay = MathF.Abs(n.Y);
        var az = MathF.Abs(n.Z);
        var seed = ax <= ay && ax <= az
            ? Vector3.UnitX
            : ay <= az ? Vector3.UnitY : Vector3.UnitZ;
        return seed - n * Vector3.Dot(n, seed);
    }

    /// <summary>
    /// Horizontal floor centred at <paramref name="center"/> with the
    /// given <paramref name="halfExtents"/>. Normal points up (+Y).
    /// </summary>
    public static WallBarrier3D Floor(Vector3 center, Vector2 halfExtents, bool oneSided = false) =>
        new(center, Vector3.UnitY, Vector3.UnitX, halfExtents) { OneSided = oneSided };

    /// <summary>
    /// Horizontal ceiling centred at <paramref name="center"/>. Normal points down (-Y).
    /// </summary>
    public static WallBarrier3D Ceiling(Vector3 center, Vector2 halfExtents, bool oneSided = false) =>
        new(center, -Vector3.UnitY, Vector3.UnitX, halfExtents) { OneSided = oneSided };

    /// <summary>
    /// Vertical wall facing <paramref name="normal"/> (a horizontal
    /// direction, projected onto the X-Z plane), centred at
    /// <paramref name="center"/>, with the given half-width and
    /// half-height. "Up" is +Y.
    /// </summary>
    public static WallBarrier3D Vertical(
        Vector3 center,
        Vector3 normal,
        float halfWidth,
        float halfHeight,
        bool oneSided = false)
    {
        // Tangent runs horizontally (perpendicular to both world-up and
        // the normal), so Bitangent ends up vertical and HalfExtents.Y
        // is the wall's height.
        var horiz = new Vector3(normal.X, 0f, normal.Z);
        if (horiz.LengthSquared() <= float.Epsilon)
            horiz = Vector3.UnitZ;
        var tangent = Vector3.Cross(Vector3.UnitY, Vector3.Normalize(horiz));
        return new WallBarrier3D(center, horiz, tangent, new Vector2(halfWidth, halfHeight))
        {
            OneSided = oneSided,
        };
    }

    /// <inheritdoc/>
    public override bool Intersects(BoundingSphere sphere)
    {
        if (sphere.IsEmpty)
            return false;

        // Signed perpendicular distance from sphere center to the wall's plane.
        var d = sphere.Center - Center;
        var n = Vector3.Dot(d, Normal);
        if (OneSided && n < 0f)
            return false;

        var r = sphere.Radius;
        if (MathF.Abs(n) > r)
            return false;

        // Project onto the wall's in-plane axes and clamp to the
        // rectangle. The resulting world-space closest point is the
        // nearest spot on the wall to the sphere centre.
        var u = Math.Clamp(Vector3.Dot(d, Tangent),   -HalfExtents.X, HalfExtents.X);
        var v = Math.Clamp(Vector3.Dot(d, Bitangent), -HalfExtents.Y, HalfExtents.Y);
        var closest = Center + Tangent * u + Bitangent * v;
        return Vector3.DistanceSquared(sphere.Center, closest) <= r * r;
    }
}
