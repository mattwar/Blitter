namespace Blitter.Blocks2D;

/// <summary>
/// A <see cref="Behavior"/> that delegates hit handling to a supplied callback.
/// Attach it to a sprite or a barrier so the entity reacts to contacts without
/// subclassing a hit method onto the entity itself. The single <see cref="OnHit"/>
/// callback fires for every overlap regardless of whether the host or the other
/// party is a sprite or a barrier — inspect the second argument's type to decide
/// how to react.
/// </summary>
public sealed class CustomHitBehavior2D : Behavior, IHittable2D
{
    /// <summary>
    /// Invoked for each entity the host overlaps this frame. Args: the host
    /// entity, then the other entity. Inspect the second argument's type
    /// (e.g. <c>other is Barrier2D</c>) to react.
    /// </summary>
    public Action<IEntity, IEntity>? OnHit { get; set; }

    void IHittable2D.OnHit(in Hit2D hit)
        => OnHit?.Invoke(this.Entity, hit.Other);
}
