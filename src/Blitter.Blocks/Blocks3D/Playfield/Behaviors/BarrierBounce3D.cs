using System.Numerics;


namespace Blitter.Blocks3D;

/// <summary>
/// Raised after a successful <see cref="BarrierBounce3D"/> bounce.
/// </summary>
/// <param name="Source">The behavior instance that raised the event.</param>
/// <param name="Self">The bouncing sprite.</param>
/// <param name="Barrier">The barrier that was bounced off.</param>
/// <param name="Normal">Contact normal, pointing from the barrier toward the sprite.</param>
public readonly record struct BarrierBounced3DEventArgs(BarrierBounce3D Source, Sprite3D Self, Barrier3D Barrier, Vector3 Normal);

/// <summary>
/// On contact with a barrier, snaps the sprite out of penetration along
/// the contact normal and reflects velocity. Final bounce composes the
/// behavior's ball-side <see cref="Restitution"/> /
/// <see cref="TangentialDamping"/> with the barrier's
/// <see cref="Barrier3D.PhysicsMaterial"/>. Shape-agnostic: any
/// <see cref="Barrier3D"/> that implements
/// <see cref="Barrier3D.HitShape"/> participates. The 3D analog of
/// <c>Blitter.Blocks2D.SurfaceBounce2D</c>.
/// </summary>
public sealed class BarrierBounce3D : Behavior, IHittable3D
{
    /// <summary>Ball-side elastic coefficient. Multiplied with the barrier's <see cref="PhysicsMaterial.Restitution"/>. 1 = perfectly elastic, 0 = sticks.</summary>
    public float Restitution { get; set; } = 1f;

    /// <summary>Ball-side tangent velocity retention. Multiplied with <c>(1 - barrier.PhysicsMaterial.Friction)</c>. 1 = frictionless ball, &lt; 1 = ball-side surface drag.</summary>
    public float TangentialDamping { get; set; } = 1f;

    /// <summary>Optional handler invoked after a successful bounce.</summary>
    public IEventHandler<BarrierBounced3DEventArgs>? Bounced { get; set; }

    public void OnHitBarrier(Sprite3D self, Barrier3D barrier, in EntityUpdateContext context)
    {
        // Normal convention: TryGetContact returns normal from b ? a;
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

        var args = new BarrierBounced3DEventArgs(this, self, barrier, normal);
        Bounced?.OnEvent(in args);
    }
}
