namespace Blitter.Blocks;

/// <summary>
/// A collection of traits and behaviors.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// The container of this entity, or <c>null</c> at the root.
    /// </summary>
    IContainerEntity? Parent { get; }

    /// <summary>
    /// The traits for this entity.
    /// </summary>
    IReadOnlyList<Trait> Traits { get; }

    /// <summary>
    /// The behaviors for this entity.
    /// </summary>
    IReadOnlyList<Behavior> Behaviors { get; }

    /// <summary>
    /// Adds a trait to this entity.
    /// </summary>
    void AddTrait(Trait trait);

    /// <summary>
    /// Adds a behavior to this entity.
    /// </summary>
    void AddBehavior(Behavior behavior);

    /// <summary>
    /// Gets the existing trait of type <typeparamref name="T"/>, 
    /// or creates and adds a new one if absent.
    /// </summary>
    T GetOrAddTrait<T>() where T : Trait, new();

    /// <summary>
    /// Reports the membership state of <paramref name="child"/> within this entity
    /// </summary>
    Containment GetContainment(IEntity child) =>
        Containment.NotContained;

    /// <summary>
    /// Reports whether <paramref name="child"/> is contained by this entity.
    /// </summary>
    bool Contains(IEntity child) =>
        GetContainment(child) == Containment.Contained;

    /// <summary>
    /// Advance this entity one tick.
    /// </summary>
    void Update(in UpdateContext context);
}

/// <summary>
/// An <see cref="IEntity"/> that owns child entities: it answers how long a
/// child has been a member and can remove a child without the caller needing
/// to know the child's concrete type or which internal list holds it.
/// </summary>
public interface IContainerEntity : IEntity
{
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
}