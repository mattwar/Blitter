using System.Numerics;


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
public sealed class BarrierBounce2D : Behavior, IHitHandler2D
{
    /// <summary>Ball-side elastic coefficient. Multiplied with the barrier's <see cref="PhysicsMaterial.Restitution"/>. 1 = perfectly elastic, 0 = sticks.</summary>
    public float Restitution { get; set; } = 1f;

    /// <summary>Ball-side tangent velocity retention. Multiplied with <c>(1 - barrier.PhysicsMaterial.Friction)</c>. 1 = frictionless ball, &lt; 1 = ball-side surface drag.</summary>
    public float TangentialDamping { get; set; } = 1f;

    /// <summary>Called after a successful bounce. Args: the bouncing entity, the barrier entity, contact normal.</summary>
    public Action<IEntity, IEntity, Vector2>? OnBounce { get; set; }

    public override void Apply(in UpdateContext context)
    {
        // No per-tick logic; bounce happens in OnHitEntity.
    }
    
    public void OnHitEntity(in Hit2D hit)
    {
        if (this.Entity is not Sprite2D self || hit.Other is not Barrier2D barrier)
            return;

        // The playfield supplies the manifold, oriented for us: the
        // normal points from the barrier surface toward the sprite. No
        // contact means the sprite was already cleared of the surface
        // (e.g. a paddle that shoved it out) — nothing to bounce.
        if (!hit.HasContact)
            return;
        var contact = hit.Contact;

        var normal = contact.Normal;
        if (contact.Penetration > 0f)
            self.Center += normal * contact.Penetration;

        // Surface velocity at the contact point. Stationary barriers
        // report zero so this collapses to the textbook reflection;
        // moving barriers (flippers, etc.) supply an ISurfaceVelocity2D
        // provider behavior that contributes their motion.
        var vSurface = barrier.TryGetBehavior<ISurfaceVelocity2D>(out var surface)
            ? surface.SurfaceVelocityAt(contact.Point)
            : Vector2.Zero;
        var mat = barrier.TryGetTrait<Surface2D>(out var pm) ? pm.Material : PhysicsMaterial.Ideal;

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
