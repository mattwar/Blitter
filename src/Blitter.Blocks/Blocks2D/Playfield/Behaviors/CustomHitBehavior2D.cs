namespace Blitter.Blocks2D;

/// <summary>
/// Per-frame callback for a <see cref="CustomHitBehavior2D"/>.
/// </summary>
public delegate void SpriteApplier(Sprite2D target, in UpdateContext context);

/// <summary>
/// A <see cref="Behavior"/> that delegates its per-frame work and hit handling
/// to supplied callbacks. Attach it to a sprite or a barrier so the entity
/// reacts to contacts without subclassing a hit method onto the entity itself.
/// The single <see cref="OnHit"/> callback fires for every overlap regardless
/// of whether the host or the other party is a sprite or a barrier — inspect
/// the second argument's type to decide how to react.
/// </summary>
public sealed class CustomHitBehavior2D : Behavior, IHitHandler2D, IUpdatable
{
    /// <summary>Invoked each frame with the host (only while it is a sprite).</summary>
    public SpriteApplier? OnApply { get; set; }

    /// <summary>
    /// Invoked for each entity the host overlaps this frame. Args: the host
    /// entity, then the other entity. Inspect the second argument's type
    /// (e.g. <c>other is Barrier2D</c>) to react.
    /// </summary>
    public Action<IEntity, IEntity>? OnHit { get; set; }

    public void Update(in UpdateContext context)
    {
        if (this.Entity is Sprite2D sprite)
            OnApply?.Invoke(sprite, in context);
    }

    public void OnHitEntity(in Hit2D hit)
        => OnHit?.Invoke(this.Entity, hit.Other);
}
