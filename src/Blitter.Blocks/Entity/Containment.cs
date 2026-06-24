namespace Blitter.Blocks;

/// <summary>
/// The membership state of an entity within a container, as reported by
/// <see cref="IEntity.GetContainment"/>. The container owns this state;
/// the child never tracks its own membership.
/// </summary>
public enum Containment
{
    /// <summary>
    /// The entity is not a member of the container.
    /// </summary>
    NotContained,

    /// <summary>
    /// The entity is a live member of the container.
    /// </summary>
    Contained,

    /// <summary>
    /// The entity is still held by the container but scheduled for removal this frame, 
    /// so it no longer participates in updates or collisions.
    /// </summary>
    Removing,
}
