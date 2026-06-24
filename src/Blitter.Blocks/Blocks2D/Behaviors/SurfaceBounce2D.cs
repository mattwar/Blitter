using System.Numerics;


namespace Blitter.Blocks2D;

/// <summary>
/// Raised after a successful <see cref="SurfaceBounce2D"/> bounce.
/// </summary>
/// <param name="Source">The behavior instance that raised the event.</param>
/// <param name="Self">The bouncing entity.</param>
/// <param name="Surface">The surface entity that was bounced off.</param>
/// <param name="Normal">Contact normal, pointing from the surface toward the entity.</param>
public readonly record struct SurfaceBounced2DEventArgs(SurfaceBounce2D Source, IEntity Self, IEntity Surface, Vector2 Normal);

/// <summary>
/// On contact with a surface, snaps the entity out of penetration along
/// the contact normal and reflects velocity. The final bounce composes the
/// behavior's ball-side <see cref="Restitution"/> /
/// <see cref="TangentialDamping"/> with the other party's
/// <see cref="Surface2D.Material"/>. The other party participates by
/// presenting a <see cref="Surface2D"/> trait — its concrete type is
/// irrelevant — and may optionally supply an <see cref="ISurfaceVelocity2D"/>
/// behavior to contribute the motion of a moving surface. The host is
/// required only to carry <see cref="Transform2D"/> (de-penetration) and
/// <see cref="Velocity2D"/> (reflection) traits — it need not be a
/// <see cref="Sprite2D"/>.
/// </summary>
public sealed class SurfaceBounce2D : Behavior, IHittable2D
{
    /// <summary>Ball-side elastic coefficient. Multiplied with the surface's <see cref="PhysicsMaterial.Restitution"/>. 1 = perfectly elastic, 0 = sticks.</summary>
    public float Restitution { get; set; } = 1f;

    /// <summary>Ball-side tangent velocity retention. Multiplied with <c>(1 - surface.PhysicsMaterial.Friction)</c>. 1 = frictionless ball, &lt; 1 = ball-side surface drag.</summary>
    public float TangentialDamping { get; set; } = 1f;

    /// <summary>Optional handler invoked after a successful bounce.</summary>
    public IEventHandler<SurfaceBounced2DEventArgs>? Bounced { get; set; }

    private Transform2D _transform = null!;
    private Velocity2D _velocity = null!;

    protected override void OnAttach(IEntity entity)
    {
        base.OnAttach(entity);
        _transform = entity.GetTrait<Transform2D>();
        _velocity = entity.GetTrait<Velocity2D>();
    }
    
    public void OnHit(in Hit2D hit)
    {
        if (this.Entity is null)
            return;

        // The other party participates only if it presents a surface to
        // bounce off — a Surface2D trait. Its concrete type (a barrier, a
        // sprite, anything) is irrelevant; the response is driven entirely
        // by that trait plus an optional ISurfaceVelocity2D behavior.
        if (hit.Other is not { } other || !other.TryGetTrait<Surface2D>(out var surface))
            return;

        // The playfield supplies the manifold, oriented for us: the
        // normal points from the surface toward the entity. No contact
        // means the entity was already cleared of the surface (e.g. a
        // paddle that shoved it out) — nothing to bounce.
        if (!hit.HasContact)
            return;
        var contact = hit.Contact;

        var normal = contact.Normal;
        if (contact.Penetration > 0f)
            _transform.Position += normal * contact.Penetration;

        // Surface velocity at the contact point. Stationary surfaces
        // report zero so this collapses to the textbook reflection;
        // moving surfaces (flippers, etc.) supply an ISurfaceVelocity2D
        // provider behavior that contributes their motion.
        var vSurface = other.TryGetBehavior<ISurfaceVelocity2D>(out var surfaceVelocity)
            ? surfaceVelocity.SurfaceVelocityAt(contact.Point)
            : Vector2.Zero;
        var mat = surface.Material;

        var vBall = _velocity.Vector;
        var vRel = vBall - vSurface;
        var along = Vector2.Dot(vRel, normal);
        if (along < 0f)
        {
            var vN = normal * along;
            var vT = vRel - vN;
            // Compose ball-side and surface-side material: behavior
            // values are the ball's defaults, surface values modulate
            // them per-surface.
            var normalScale = Restitution * mat.Restitution;
            var tangentMul = TangentialDamping * (1f - mat.Friction);
            vRel = vT * tangentMul - vN * normalScale;
            vBall = vRel + vSurface;
            if (mat.KickSpeed != 0f)
                vBall += normal * mat.KickSpeed;
            _velocity.Vector = vBall;
        }

        var args = new SurfaceBounced2DEventArgs(this, this.Entity, other, normal);
        Bounced?.OnEvent(in args);
    }
}
