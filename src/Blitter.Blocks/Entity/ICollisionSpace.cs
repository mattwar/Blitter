namespace Blitter.Blocks;

/// <summary>
/// A collision space that an entity or behavior can implement to determine
/// how the <see cref="Updater"/> should apply step counting.
/// </summary>
public interface ICollisionSpace
{
    /// <summary>
    /// Returns how many update/collision substeps should run for this update.
    /// </summary>
    int GetCollisionSubstepCount(in EntityUpdateContext context) => 1;

    /// <summary>
    /// Resolves collisions among this space's entities for a single substep,
    /// dispatching any resulting contacts.
    /// </summary>
    void ResolveCollisions();
}
