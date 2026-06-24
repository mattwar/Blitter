using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// On contact with a <see cref="LineBarrier2D"/>, snaps the sprite out
/// of penetration along the barrier's outward normal and cancels the
/// component of velocity pointing into the surface. Tangential motion
/// (sliding along the barrier) is preserved, so this works for floors,
/// walls, and ceilings simultaneously.
/// </summary>
public sealed class BarrierStop2D : Behavior, IHitHandler2D, IUpdatable
{
    /// <summary>
    /// True while the sprite is resting on a floor (a barrier whose
    /// outward normal points more up than sideways). Updated each frame
    /// from contacts seen in the previous frame, so jump input can read
    /// this during <see cref="Update"/>.
    /// </summary>
    public bool IsGrounded { get; private set; }

    // Floor contact registered by OnHitBarrier on the previous frame.
    // Update consumes and clears it; OnHitBarrier (which runs after
    // Update within the same frame) sets it for next frame.
    private bool _floorContactSeen;

    public void Update(in UpdateContext context)
    {
        IsGrounded = _floorContactSeen;
        _floorContactSeen = false;
    }

    public void OnHitEntity(in Hit2D hit)
    {
        if (this.Entity is not Sprite2D self || hit.Other is not LineBarrier2D)
            return;

        // The playfield supplies the manifold, oriented for us: the
        // normal points from the surface toward the sprite, so it pushes
        // out correctly from either side of a two-sided segment.
        if (!hit.HasContact)
            return;
        var normal = hit.Contact.Normal;

        // Snap out of penetration along the contact normal.
        if (hit.Contact.Penetration > 0f)
            self.Center += normal * hit.Contact.Penetration;

        // Zero the component of velocity heading INTO the surface.
        // Tangential motion is preserved.
        var v = Sprite2D.GetVelocity(self.Speed, self.Heading);
        var along = Vector2.Dot(v, normal);
        if (along < 0f)
        {
            v -= normal * along;
            (self.Speed, self.Heading) = Sprite2D.GetSpeedAndHeading(v);
        }

        // Floor-ish contact: normal pointing more up than sideways.
        if (normal.Y < -0.7f)
            _floorContactSeen = true;
    }
}
