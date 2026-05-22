using System.Numerics;

namespace Blitter;

/// <summary>
/// World-space placement of 2D geometry: position, rotation, uniform
/// scale, and optional flip. Bundled into one struct so the visual
/// pose and the collision pose can stay in sync.
/// </summary>
public readonly struct Pose2D
{
    /// <summary>Identity pose: at the origin, unrotated, unit scale, no flip.</summary>
    public static readonly Pose2D Identity = new(Vector2.Zero);

    /// <summary>World-space position of the local origin.</summary>
    public readonly Vector2 Position;
    /// <summary>Rotation in degrees (0 = unrotated).</summary>
    public readonly float Rotation;
    /// <summary>Uniform scale applied to the local geometry.</summary>
    public readonly float Scale;
    /// <summary>Mirror applied to the local geometry before rotation.</summary>
    public readonly FlipMode Flipped;

    public Pose2D(
        Vector2 position,
        float rotation = 0f,
        float scale = 1f,
        FlipMode flipped = FlipMode.None)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Flipped = flipped;
    }

    /// <summary>
    /// Returns <paramref name="local"/> mirrored according to
    /// <see cref="Flipped"/>. Use on image-local points before
    /// applying rotation and translation.
    /// </summary>
    public Vector2 ApplyFlip(Vector2 local) => Flipped switch
    {
        FlipMode.Horizontal => new Vector2(-local.X, local.Y),
        FlipMode.Vertical => new Vector2(local.X, -local.Y),
        _ => local,
    };

    /// <summary>
    /// Transforms an image-local point to world space: 
    /// flip, then scale, then rotation, then translation. 
    /// Matches <see cref="Renderer2D"/>'s draw order.
    /// </summary>
    public Vector2 Transform(Vector2 local)
    {
        local = ApplyFlip(local) * Scale;
        var rad = Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        return Position + new Vector2(
            local.X * cos - local.Y * sin,
            local.X * sin + local.Y * cos
            );
    }
}
