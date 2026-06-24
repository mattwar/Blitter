namespace Blitter.Blocks2D;

/// <summary>
/// Capability for a <see cref="Behavior"/> that responds to collisions the
/// <see cref="PlayField2D"/> detects. The playfield invokes this on each
/// behavior of an entity involved in a hit; the entity itself exposes no
/// collision API. The same callback fires whether the host is a sprite or a
/// barrier and whether the other party is a sprite or a barrier — the receiver
/// inspects the other entity (e.g. <c>other is Barrier2D</c>) to decide how to
/// react. The hosting entity is available as <see cref="Behavior.Entity"/>.
/// </summary>
public interface IHittable2D
{
    /// <summary>
    /// Invoked when the hosting entity's hit shape overlaps another entity's
    /// during the playfield's collision detection. <paramref name="hit"/>
    /// carries the other party and the contact manifold (oriented for this
    /// receiver), so handlers need not recompute it.
    /// </summary>
    void OnHit(in Hit2D hit);
}

/// <summary>
/// Forwards playfield-detected hits to an entity's <see cref="IHittable2D"/>
/// behaviors. Collision dispatch lives with the playfield, not on the entity.
/// </summary>
internal static class HitDispatch2D
{
    public static void Dispatch(IEntity self, in Hit2D hit)
    {
        var behaviors = self.Behaviors;
        for (int i = 0; i < behaviors.Count; i++)
        {
            if (behaviors[i] is IHittable2D hittable)
            {
                hittable.OnHit(in hit);  
            }            
        }
    }
}
