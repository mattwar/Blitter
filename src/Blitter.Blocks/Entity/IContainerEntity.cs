namespace Blitter.Blocks;

/// <summary>
/// An <see cref="IEntity"/> that contains other entities.
/// </summary>
public interface IContainerEntity : IEntity
{
    /// <summary>
    /// The contained entities.
    /// </summary>
    IReadOnlyList<IEntity> Entities { get; }

    /// <summary>
    /// Adds <paramref name="entity"/> to this container.
    /// </summary>
    void AddEntity(IEntity entity);

    /// <summary>
    /// Removes <paramref name="entity"/> from this container. No-op if the
    /// entity is not a member.
    /// </summary>
    void RemoveEntity(IEntity entity);

    /// <summary>
    /// Reports the membership state of <paramref name="entity"/> within this container.
    /// </summary>
    Containment GetContainment(IEntity entity) =>
        Entities.Contains(entity) ? Containment.Contained : Containment.NotContained;

    /// <summary>
    /// Reports whether <paramref name="entity"/> is contained by this container.
    /// </summary>
    bool Contains(IEntity entity) =>
        GetContainment(entity) == Containment.Contained;
}