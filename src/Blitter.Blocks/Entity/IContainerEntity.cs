namespace Blitter.Blocks;

/// <summary>
/// An <see cref="IEntity"/> that owns child entities: it answers how long a
/// child has been a member and can remove a child without the caller needing
/// to know the child's concrete type or which internal list holds it.
/// </summary>
public interface IContainerEntity : IEntity
{
    /// <summary>
    /// The child entities owned by this container.
    /// </summary>
    IReadOnlyList<IEntity> Entities { get; }

    /// <summary>
    /// Adds <paramref name="child"/> to this container.
    /// </summary>
    void AddEntity(IEntity child);

    /// <summary>
    /// How long <paramref name="child"/> has been a member of this container,
    /// or <see cref="TimeSpan.Zero"/> if it is not a tracked member.
    /// </summary>
    TimeSpan GetAge(IEntity child);

    /// <summary>
    /// Removes <paramref name="child"/> from this container. No-op if the
    /// child is not a member.
    /// </summary>
    void RemoveEntity(IEntity child);

    /// <summary>
    /// Reports the membership state of <paramref name="child"/> within this container.
    /// </summary>
    Containment GetContainment(IEntity child) =>
        Containment.NotContained;

    /// <summary>
    /// Reports whether <paramref name="child"/> is contained by this container.
    /// </summary>
    bool Contains(IEntity child) =>
        GetContainment(child) == Containment.Contained;
}