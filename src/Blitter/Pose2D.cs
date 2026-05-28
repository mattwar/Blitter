using System.Numerics;

namespace Blitter;

/// <summary>
/// World-space placement of 2D geometry: position, rotation, and scale. 
/// </summary>
public readonly struct Pose2D
{
    /// <summary>Identity pose: at the origin, unrotated, unit scale.</summary>
    public static readonly Pose2D Identity = new(Vector2.Zero);

    /// <summary>World-space position of the local origin.</summary>
    public readonly Vector2 Position;
    /// <summary>Rotation in degrees (0 = unrotated).</summary>
    public readonly float Rotation;
    /// <summary>Uniform scale applied to the local geometry.</summary>
    public readonly float Scale;

    public Pose2D(
        Vector2 position,
        float rotation = 0f,
        float scale = 1f)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    /// <summary>
    /// Transforms an image-local point to world space: 
    /// scale, then rotation, then translation. 
    /// Matches <see cref="Renderer2D"/>'s draw order.
    /// </summary>
    public Vector2 Transform(Vector2 local)
    {
        local *= Scale;
        var rad = Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        return Position + new Vector2(
            local.X * cos - local.Y * sin,
            local.X * sin + local.Y * cos
            );
    }
}
