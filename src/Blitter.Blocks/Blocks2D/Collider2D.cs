namespace Blitter.Blocks2D;

/// <summary>
/// Runs the playfield's 2D collision pass: sprite-vs-sprite and
/// sprite-vs-barrier overlap detection, dispatching each contact to the
/// involved entities' <see cref="IHittable2D"/> behaviors.
/// </summary>
/// <remarks>
/// Entity-agnostic by design: both lists are typed as
/// <see cref="IEntity"/> and every hit shape is sourced uniformly from the
/// entity's <see cref="IColliderShape2D"/> behavior — never from a property on
/// a concrete sprite/barrier type. The only host-specific knowledge it needs
/// is liveness (handlers can remove entities mid-pass), supplied as a
/// predicate so it can be re-queried between dispatches.
/// </remarks>
public sealed class Collider2D
{
    private readonly Func<IEntity, bool> _isLive;

    public Collider2D(Func<IEntity, bool> isLive) => _isLive = isLive;

    /// <summary>
    /// Detects overlaps among <paramref name="sprites"/> and between sprites
    /// and <paramref name="barriers"/>, dispatching hits as it goes. Barriers
    /// never collide with each other. Shapes are re-read between dispatches so
    /// a handler that moves an entity is reflected immediately.
    /// </summary>
    public void Collide(IReadOnlyList<IEntity> sprites, IReadOnlyList<IEntity> barriers)
    {
        // sprite-vs-sprite
        for (int i = 0; i < sprites.Count; i++)
        {
            var a = sprites[i];
            if (!_isLive(a))
                continue;
            if (!TryGetHitShape(a, out var aShape) || aShape.BoundingCircle.Radius <= 0f)
                continue;

            for (int j = i + 1; j < sprites.Count; j++)
            {
                if (!_isLive(a))
                    break;

                var b = sprites[j];
                if (!_isLive(b))
                    continue;
                if (!TryGetHitShape(b, out var bShape) || bShape.BoundingCircle.Radius <= 0f)
                    continue;
                if (!aShape.TestHit(bShape))
                    continue;

                // Manifold normal points from b's surface toward a.
                var hasContact = aShape.TryGetContact(bShape, out var contact);
                var hitForA = new Hit2D(b, contact, hasContact);
                HitDispatch2D.Dispatch(a, in hitForA);
                if (_isLive(a) && _isLive(b))
                {
                    var hitForB = new Hit2D(a, hasContact ? contact.Flipped() : default, hasContact);
                    HitDispatch2D.Dispatch(b, in hitForB);
                }
            }
        }

        // sprite-vs-barrier
        if (barriers.Count == 0)
            return;

        for (int s = 0; s < sprites.Count; s++)
        {
            var sprite = sprites[s];
            if (!_isLive(sprite))
                continue;
            if (!TryGetHitShape(sprite, out var spriteShape) || spriteShape.BoundingCircle.IsEmpty)
                continue;

            for (int k = 0; k < barriers.Count; k++)
            {
                if (!_isLive(sprite))
                    break;
                var barrier = barriers[k];
                // Re-read each time: the previous barrier handler
                // may have moved the sprite.
                if (!TryGetHitShape(sprite, out var sShape)
                    || !TryGetHitShape(barrier, out var barrierShape)
                    || !sShape.TestHit(barrierShape))
                    continue;

                // Barrier reacts first so any state change it
                // makes (re-arming, lowering a drop target,
                // swapping its Material) is visible to the
                // sprite's bounce resolution on the same frame.
                // Manifold here is oriented for the barrier: normal
                // points from the sprite toward the barrier surface.
                var barrierHas = sShape.TryGetContact(barrierShape, out var barrierContact);
                var barrierHit = new Hit2D(
                    sprite,
                    barrierHas ? barrierContact.Flipped() : default,
                    barrierHas);
                HitDispatch2D.Dispatch(barrier, in barrierHit);
                if (!_isLive(sprite))
                    continue;

                // Recompute fresh for the sprite: the barrier handler
                // may have moved it, so the bounce sees post-reaction
                // geometry (a paddle that shoved the ball clear now
                // reports no contact -> no spurious second bounce).
                // Normal points from the barrier surface toward the
                // sprite — the "out of the surface" direction.
                if (!TryGetHitShape(sprite, out var sShapeNow)
                    || !TryGetHitShape(barrier, out var barrierShapeNow))
                    continue;
                var spriteHas = sShapeNow.TryGetContact(barrierShapeNow, out var spriteContact);
                var spriteHit = new Hit2D(barrier, spriteContact, spriteHas);
                HitDispatch2D.Dispatch(sprite, in spriteHit);
            }
        }
    }

    /// <summary>
    /// Resolves an entity's world-space hit shape from its
    /// <see cref="IColliderShape2D"/> behavior. Returns <c>false</c> when the
    /// entity carries no collider.
    /// </summary>
    public static bool TryGetHitShape(IEntity entity, out PosedHitShape2D shape)
    {
        if (entity.TryGetBehavior<IColliderShape2D>(out var collider))
        {
            shape = collider.GetShape();
            return true;
        }

        shape = default;
        return false;
    }
}
