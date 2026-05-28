using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// A spherical barrier — bumpers, planets, posts in 3D. The 3D analog
/// of <c>Blitter.Blocks2D.CircleBarrier2D</c>.
/// </summary>
public class SphereBarrier3D : Barrier3D
{
    public Vector3 Center { get; }
    public float Radius { get; }

    public SphereBarrier3D(Vector3 center, float radius)
    {
        Center = center;
        Radius = radius < 0f ? 0f : radius;
    }

    public SphereBarrier3D(float x, float y, float z, float radius)
        : this(new Vector3(x, y, z), radius) { }

    /// <inheritdoc/>
    public override PosedHitShape3D HitShape =>
        new(new SphereHitShape3D(Vector3.Zero, Radius),
            new Pose3D(Center, Quaternion.Identity, 1f));
}
