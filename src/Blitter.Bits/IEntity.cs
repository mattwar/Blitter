namespace Blitter.Bits;

/// <summary>
/// A collection of traits and behaviors.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// The container of this entity, or <c>null</c> at the root.
    /// </summary>
    IEntity? Parent { get; }

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


public interface IContainerEntity : IEntity
{
    /// <summary>
    /// The entities directly contained by this container.
    /// </summary>
    IReadOnlyList<IEntity> Children { get; }

    /// <summary>
    /// Adds a child entity to this container.
    /// </summary>
    void AddChild(IEntity child);
}