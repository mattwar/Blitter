using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// Rate-of-change of an entity's placement: linear <see cref="Velocity"/>
/// plus <see cref="AngularVelocity"/>. The canonical velocity behind
/// <see cref="Sprite3D"/>'s accessors; integrated by a motion behavior,
/// not by the trait itself. The 3D analog of <c>Blitter.Blocks2D.Velocity2D</c>.
/// </summary>
public sealed class Velocity3D : Trait
{
    /// <summary>Linear velocity in world units per second.</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>
    /// Angular velocity as an axis-times-radians-per-second vector. The
    /// vector's direction is the rotation axis; its length is the angular speed.
    /// </summary>
    public Vector3 AngularVelocity { get; set; }
}
