using System.Numerics;
 
namespace Blitter.Blocks3D;
using Bits;

/// <summary>
/// Rotates the sprite continuously around its vertical axis (Y-axis). 
/// This is useful for items like spinning coins, rotating signs, or 
/// other objects that need a constant rotation effect.
/// </summary>
public sealed class Spin3D : SpriteBehavior3D
{
    /// <summary>
    /// The speed of the spin in radians per second.
    /// </summary>
    public float RotationSpeed { get; set; }

    private Sprite3D _target = null!;

    protected override void OnAttach(IEntity entity)
    {
        _target = (Sprite3D)entity;
    }

    public override void Apply(in UpdateContext context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f || RotationSpeed == 0f)
            return;

        // Rotate the current orientation by a small amount around the Y axis.
        _target.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, RotationSpeed * dt);
    }
}
