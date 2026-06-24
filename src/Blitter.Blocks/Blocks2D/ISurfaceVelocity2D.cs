using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// A behavior capability that supplies the world-space velocity of a barrier's
/// surface at a contact point. <see cref="SurfaceBounce2D"/> queries the
/// barrier's behaviors for one of these so a moving barrier (flipper, conveyor,
/// rotating arm) transfers its motion into the bounce. Absent provider means a
/// stationary surface (<see cref="Vector2.Zero"/>).
/// </summary>
public interface ISurfaceVelocity2D
{
    /// <summary>
    /// World-space velocity (units per second) of the surface at
    /// <paramref name="point"/>.
    /// </summary>
    Vector2 SurfaceVelocityAt(Vector2 point);
}
