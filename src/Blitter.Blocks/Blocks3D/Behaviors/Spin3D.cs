using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// Rotates the entity continuously around its vertical axis (Y-axis). 
/// This is useful for items like spinning coins, rotating signs, or 
/// other objects that need a constant rotation effect.
/// </summary>
public sealed class Spin3D : Behavior, IUpdatable
{
    /// <summary>
    /// The speed of the spin in radians per second.
    /// </summary>
    public float RotationSpeed { get; set; }

    private Transform3D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _transform = entity.GetOrAddTrait<Transform3D>();
    }

    public void Update(in EntityUpdateContext context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f || RotationSpeed == 0f)
            return;

        // Rotate the current orientation by a small amount around the Y axis.
        _transform.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, RotationSpeed * dt);
    }
}
