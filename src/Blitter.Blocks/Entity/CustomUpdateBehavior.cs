namespace Blitter.Blocks;

/// <summary>
/// Per-frame callback for a <see cref="CustomUpdateBehavior"/>.
/// </summary>
public delegate void EntityUpdater(IEntity entity, in UpdateContext context);

/// <summary>
/// A <see cref="Behavior"/> that runs a supplied callback each frame. Attach it
/// to any entity to add ad-hoc per-frame logic without writing a dedicated
/// behavior class.
/// </summary>
public sealed class CustomUpdateBehavior : Behavior, IUpdatable
{
    /// <summary>Invoked each frame with the host entity.</summary>
    public EntityUpdater? OnUpdate { get; set; }

    public void Update(in UpdateContext context)
        => OnUpdate?.Invoke(this.Entity, in context);
}
