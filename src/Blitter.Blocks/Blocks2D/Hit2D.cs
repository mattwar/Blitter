namespace Blitter.Blocks2D;

/// <summary>
/// The information passed to an <see cref="IHitHandler2D.OnHitEntity"/> call:
/// the other party in the collision plus the contact manifold the playfield
/// computed for it. The manifold is always oriented relative to the receiver —
/// <see cref="HitContact2D.Normal"/> points from <see cref="Other"/> toward the
/// hosting entity, i.e. the "out of the surface" direction for the receiver.
/// </summary>
/// <remarks>
/// <see cref="HasContact"/> is <c>false</c> when the pair overlapped (the hit
/// fired) but no closed-form manifold was available — a grazing/zero-penetration
/// touch, or a shape pair without a contact solver. Handlers that need the
/// manifold should check it before reading <see cref="Contact"/>.
/// </remarks>
public readonly struct Hit2D
{
    /// <summary>The other entity involved in the collision.</summary>
    public IEntity Other { get; }

    /// <summary>
    /// Contact manifold oriented for the receiver (normal points from
    /// <see cref="Other"/> toward the hosting entity). Only meaningful when
    /// <see cref="HasContact"/> is <c>true</c>.
    /// </summary>
    public HitContact2D Contact { get; }

    /// <summary>True when <see cref="Contact"/> holds a valid manifold.</summary>
    public bool HasContact { get; }

    public Hit2D(IEntity other, HitContact2D contact, bool hasContact)
    {
        Other = other;
        Contact = contact;
        HasContact = hasContact;
    }
}
