using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

/// <summary>
/// A headless <see cref="Sprite3D"/> whose collision shape is a plain
/// sphere — no <see cref="Visual3D"/>/GPU needed. Lets barrier and
/// bounce logic be exercised in a unit test.
/// </summary>
internal sealed class BallSprite3D : Sprite3D
{
    public float Radius { get; set; } = 0.5f;

    public override PosedHitShape3D HitShape =>
        new(new SphereHitShape3D(Vector3.Zero, Radius),
            new Pose3D(Position, Orientation, Scale));
}
