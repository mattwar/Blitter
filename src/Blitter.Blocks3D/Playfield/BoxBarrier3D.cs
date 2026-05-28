using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A solid oriented box — crates, blocks, room interiors. Defaults to
/// axis-aligned when constructed with <see cref="Quaternion.Identity"/>.
/// </summary>
public class BoxBarrier3D : Barrier3D
{
    /// <summary>World-space centre of the box.</summary>
    public Vector3 Center { get; }

    /// <summary>Orientation of the box's local axes.</summary>
    public Quaternion Rotation { get; }

    /// <summary>Half-size along each local axis (X / Y / Z).</summary>
    public Vector3 HalfExtents { get; }

    public BoxBarrier3D(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        Center = center;
        HalfExtents = Vector3.Max(halfExtents, Vector3.Zero);
        Rotation = rotation;
    }

    public BoxBarrier3D(Vector3 center, Vector3 halfExtents)
        : this(center, halfExtents, Quaternion.Identity) { }

    /// <summary>Builds an axis-aligned box from a <see cref="BoundingBox"/>.</summary>
    public BoxBarrier3D(BoundingBox box)
        : this(box.Center, box.Extents, Quaternion.Identity) { }

    /// <inheritdoc/>
    public override PosedHitShape3D HitShape =>
        new(new BoxHitShape3D(Vector3.Zero, HalfExtents),
            new Pose3D(Center, Rotation, 1f));
}
