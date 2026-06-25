using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// On contact with a surface, snaps the entity out of penetration along
/// the surface's outward normal and cancels the component of velocity
/// pointing into it. Tangential motion (sliding along the surface) is
/// preserved, so this works for floors, walls, and ceilings
/// simultaneously. The other party participates by presenting a
/// <see cref="Surface2D"/> trait — its concrete type (any barrier shape,
/// or another entity) is irrelevant, and no <see cref="Surface2D.Material"/>
/// is read, so the trait acts purely as a "solid surface" marker. The host
/// need only carry <see cref="Transform2D"/> (de-penetration) and
/// <see cref="Velocity2D"/> (velocity cancel) traits — it need not be a
/// <see cref="Sprite2D"/>.
/// </summary>
public sealed class SurfaceStop2D : Behavior, IHittable2D, IUpdatable
{
    /// <summary>
    /// True while the entity is resting on a floor (a surface whose
    /// outward normal points more up than sideways). Updated each frame
    /// from contacts seen in the previous frame, so jump input can read
    /// this during <see cref="Update"/>.
    /// </summary>
    public bool IsGrounded { get; private set; }

    // Floor contact registered by OnHit on the previous frame.
    // Update consumes and clears it; OnHit (which runs after
    // Update within the same frame) sets it for next frame.
    private bool _floorContactSeen;

    private Transform2D _transform = null!;
    private Velocity2D _velocity = null!;

    protected override void OnAttach(IEntity entity)
    {
        base.OnAttach(entity);
        _transform = entity.GetTrait<Transform2D>();
        _velocity = entity.GetTrait<Velocity2D>();
    }

    public void Update(in EntityUpdateContext context)
    {
        IsGrounded = _floorContactSeen;
        _floorContactSeen = false;
    }

    public void OnHit(in Hit2D hit)
    {
        if (this.Entity is null)
            return;

        // The other party participates only if it presents a surface to
        // stop against — a Surface2D trait. Its concrete type (any barrier
        // shape, or another entity) is irrelevant, and no material data is
        // read; the trait is just a "solid surface" marker.
        if (hit.Other is not { } other || !other.TryGetTrait<Surface2D>(out _))
            return;

        // The playfield supplies the manifold, oriented for us: the
        // normal points from the surface toward the entity, so it pushes
        // out correctly from either side of a two-sided segment.
        if (!hit.HasContact)
            return;
        var normal = hit.Contact.Normal;

        // Snap out of penetration along the contact normal.
        if (hit.Contact.Penetration > 0f)
            _transform.Position += normal * hit.Contact.Penetration;

        // Zero the component of velocity heading INTO the surface.
        // Tangential motion is preserved.
        var v = _velocity.Vector;
        var along = Vector2.Dot(v, normal);
        if (along < 0f)
        {
            v -= normal * along;
            _velocity.Vector = v;
        }

        // Floor-ish contact: normal pointing more up than sideways.
        if (normal.Y < -0.7f)
            _floorContactSeen = true;
    }
}
