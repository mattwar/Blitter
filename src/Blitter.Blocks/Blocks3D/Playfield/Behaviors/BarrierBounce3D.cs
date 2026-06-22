using System.Numerics;


namespace Blitter.Blocks3D;

/// <summary>
/// On contact with a barrier, snaps the sprite out of penetration along
/// the contact normal and reflects velocity. Final bounce composes the
/// behavior's ball-side <see cref="Restitution"/> /
/// <see cref="TangentialDamping"/> with the barrier's
/// <see cref="Barrier3D.PhysicsMaterial"/>. Shape-agnostic: any
/// <see cref="Barrier3D"/> that implements
/// <see cref="Barrier3D.HitShape"/> participates. The 3D analog of
/// <c>Blitter.Blocks2D.BarrierBounce2D</c>.
/// </summary>
public sealed class BarrierBounce3D : Behavior, IHitHandler3D
{
    /// <summary>Ball-side elastic coefficient. Multiplied with the barrier's <see cref="PhysicsMaterial.Restitution"/>. 1 = perfectly elastic, 0 = sticks.</summary>
    public float Restitution { get; set; } = 1f;

    /// <summary>Ball-side tangent velocity retention. Multiplied with <c>(1 - barrier.PhysicsMaterial.Friction)</c>. 1 = frictionless ball, &lt; 1 = ball-side surface drag.</summary>
    public float TangentialDamping { get; set; } = 1f;

    /// <summary>Called after a successful bounce. Args: sprite, barrier, contact normal.</summary>
    public Action<Sprite3D, Barrier3D, Vector3>? OnBounce { get; set; }

    public override void Apply(in UpdateContext context)
    {
        // no nothing - real work happens in OnHitBarrier.
    }

    public void OnHitBarrier(Sprite3D self, Barrier3D barrier, in UpdateContext context)
    {
        // Normal convention: TryGetContact returns normal from b → a;
        // here a = self.HitShape, b = barrier.HitShape, so `contact.Normal`
        // points from the barrier surface toward the sprite.
        if (!self.HitShape.TryGetContact(barrier.HitShape, out var contact))
            return;

        var normal = contact.Normal;
        if (contact.Penetration > 0f)
            self.Position += normal * contact.Penetration;

        // Use the contact point on the barrier surface; stationary
        // barriers report zero velocity here, so the textbook
        // reflection drops out, while moving barriers (sliding
        // paddles, etc.) feed their motion into the bounce.
        var vSurface = barrier.SurfaceVelocityAt(contact.Point);
        var mat = barrier.PhysicsMaterial;

        var vBall = self.Velocity;
        var vRel = vBall - vSurface;
        var along = Vector3.Dot(vRel, normal);
        if (along < 0f)
        {
            var vN = normal * along;
            var vT = vRel - vN;
            var normalScale = Restitution * mat.Restitution;
            var tangentMul = TangentialDamping * (1f - mat.Friction);
            vRel = vT * tangentMul - vN * normalScale;
            vBall = vRel + vSurface;
            if (mat.KickSpeed != 0f)
                vBall += normal * mat.KickSpeed;
            self.Velocity = vBall;
        }

        OnBounce?.Invoke(self, barrier, normal);
    }
}
