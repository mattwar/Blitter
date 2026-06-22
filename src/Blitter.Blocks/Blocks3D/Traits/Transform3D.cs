using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// World-space placement of an entity: position, orientation, and scale.
/// The canonical pose behind <see cref="Sprite3D"/>'s friendly accessors;
/// read <see cref="Pose"/> to draw or hit-test. The 3D analog of
/// <c>Blitter.Blocks2D.Transform2D</c>.
/// </summary>
public sealed class Transform3D : Trait
{
    /// <summary>World-space position of the entity's local origin.</summary>
    public Vector3 Position { get; set; }

    /// <summary>Orientation of the entity's local axes in world space.</summary>
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    /// <summary>Uniform scale factor applied to the visual and hit shape.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>The placement as a <see cref="Pose3D"/> for drawing and hit-testing.</summary>
    public Pose3D Pose => new(Position, Orientation, Scale);
}
