using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks2D;

/// <summary>
/// On contact with a barrier, snaps the sprite out of penetration along
/// the contact normal and reflects velocity. Final bounce composes the
/// behavior's ball-side <see cref="Restitution"/> /
/// <see cref="TangentialDamping"/> with the barrier's
/// <see cref="Barrier2D.PhysicsMaterial"/>. Shape-agnostic: any
/// <see cref="Barrier2D"/> whose <see cref="Barrier2D.HitShape"/>
/// reports a contact participates.
/// </summary>
public sealed class BarrierBounce2D : SpriteBehavior2D
{
    /// <summary>Ball-side elastic coefficient. Multiplied with the barrier's <see cref="PhysicsMaterial.Restitution"/>. 1 = perfectly elastic, 0 = sticks.</summary>
    public float Restitution { get; set; } = 1f;

    /// <summary>Ball-side tangent velocity retention. Multiplied with <c>(1 - barrier.PhysicsMaterial.Friction)</c>. 1 = frictionless ball, &lt; 1 = ball-side surface drag.</summary>
    public float TangentialDamping { get; set; } = 1f;

    /// <summary>Called after a successful bounce. Args: sprite, barrier, contact normal.</summary>
    public Action<Sprite2D, Barrier2D, Vector2>? OnBounce { get; set; }

    public override void Apply(in UpdateContext context)
    {
        // No per-tick logic; bounce happens in OnHitBarrier.
    }
    
    public override void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext context)
    {
        // Normal convention: TryGetContact returns normal from b → a;
        // here a = self.HitShape, b = barrier.HitShape, so `contact.Normal`
        // points from the barrier surface toward the sprite.
        if (!self.HitShape.TryGetContact(barrier.HitShape, out var contact))
            return;

        var normal = contact.Normal;
        if (contact.Penetration > 0f)
            self.Center += normal * contact.Penetration;

        // Surface velocity at the contact point. Stationary barriers
        // report zero so this collapses to the textbook reflection;
        // moving barriers (flippers, etc.) contribute their motion.
        var vSurface = barrier.SurfaceVelocityAt(contact.Point);
        var mat = barrier.PhysicsMaterial;

        var vBall = Sprite2D.GetVelocity(self.Speed, self.Heading);
        var vRel = vBall - vSurface;
        var along = Vector2.Dot(vRel, normal);
        if (along < 0f)
        {
            var vN = normal * along;
            var vT = vRel - vN;
            // Compose ball-side and barrier-side material: behavior
            // values are the ball's defaults, barrier values modulate
            // them per-surface.
            var normalScale = Restitution * mat.Restitution;
            var tangentMul = TangentialDamping * (1f - mat.Friction);
            vRel = vT * tangentMul - vN * normalScale;
            vBall = vRel + vSurface;
            if (mat.KickSpeed != 0f)
                vBall += normal * mat.KickSpeed;
            (self.Speed, self.Heading) = Sprite2D.GetSpeedAndHeading(vBall);
        }

        OnBounce?.Invoke(self, barrier, normal);
    }
}
