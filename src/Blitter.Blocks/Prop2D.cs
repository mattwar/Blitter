using Blitter.Bits;

namespace Blitter.Blocks;

public abstract class Prop2D : IUpdatable<UpdateContext2D>, IDrawable2D
{
    /// <summary>Advance one tick. Return false to indicate this prop wants to be removed.</summary>
    public abstract bool Update(in UpdateContext2D context);

    /// <summary>Issue draws for this prop's current state.</summary>
    public abstract void Draw(Renderer2D renderer);

    /// <summary>
    /// When set to <c>false</c> the prop is marked for removal; its
    /// owning container drops it on its next update cycle and stops
    /// drawing it. One-way: don't flip back to <c>true</c> to revive a
    /// prop -- re-add via the container's <c>Add</c> API instead.
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// The <see cref="Container2D"/> this prop currently belongs to,
    /// or <c>null</c> if it has not been added to one. A prop has at
    /// most one container at a time; calling <see cref="Container2D.Add"/>
    /// on a new container silently reparents it.
    /// </summary>
    public Container2D? Container { get; internal set; }

    // Container-local timestamp captured when this prop joined its
    // current container. Reset on each Add (including reparent).
    internal TimeSpan _spawnedAt;

    /// <summary>
    /// How long this prop has been a member of its current
    /// <see cref="Container"/>. Returns <see cref="TimeSpan.Zero"/>
    /// when the prop is unparented. Reset to zero each time the prop
    /// is added to a container (including reparenting), so freshly
    /// spawned props can be granted a grace period from collision or
    /// other interactions.
    /// </summary>
    public TimeSpan Age => Container is { } c ? c.Elapsed - _spawnedAt : TimeSpan.Zero;

    /// <summary>
    /// World-space hit shape for this prop, or <c>null</c> if the prop
    /// is not collidable. Used by hit-detection behaviors to test for
    /// overlap with other props. Defaults to <c>null</c>; subclasses
    /// expose collider data (e.g. <see cref="Sprite2D.HitRadius"/>) and
    /// override this to compose the shape from that data.
    /// </summary>
    public virtual BoundingCircle? HitCircle => null;

    /// <summary>
    /// Called by the owning <see cref="Container2D"/> when this prop's
    /// <see cref="HitCircle"/> intersects another prop's during the
    /// container's collision pass. Default is a no-op.
    /// </summary>
    public virtual void OnCollision(Prop2D other, in UpdateContext2D context) { }

    // IUpdatable<TCtx> contract: forwards to the bool-returning override.
    // The Container/Scene loop reads the bool to drive lifecycle; the
    // generic interface contract is "advance once" and ignores the return.
    void IUpdatable<UpdateContext2D>.Update(in UpdateContext2D context) => Update(context);
}
