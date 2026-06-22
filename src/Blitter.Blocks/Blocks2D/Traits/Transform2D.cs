using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks2D;

/// <summary>
/// World-space placement of an entity: position, rotation, and scale.
/// The canonical pose behind <see cref="Sprite2D"/>'s friendly accessors;
/// read <see cref="Pose"/> to draw or hit-test.
/// </summary>
public sealed class Transform2D : Trait
{
    /// <summary>World-space position of the entity's center.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Orientation in degrees (0 = unrotated).</summary>
    public float Rotation { get; set; }

    /// <summary>Uniform scale factor applied to the visual.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>The placement as a <see cref="Pose2D"/> for drawing and hit-testing.</summary>
    public Pose2D Pose => new(Position, Rotation, Scale);
}
