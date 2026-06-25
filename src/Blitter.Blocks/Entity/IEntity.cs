namespace Blitter.Blocks;

/// <summary>
/// A collection of traits and behaviors.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// The container of this entity, or <c>null</c> at the root.
    /// </summary>
    IContainer? Container { get; }

    /// <summary>
    /// The behaviors for this entity.
    /// </summary>
    IReadOnlyList<Behavior> Behaviors { get; }

    /// <summary>
    /// The traits for this entity.
    /// </summary>
    IReadOnlyList<Trait> Traits { get; }

    /// <summary>
    /// Gets the existing behavior of type <typeparamref name="T"/>, 
    /// or creates and adds a new one if absent.
    /// </summary>
    T GetOrAddBehavior<T>() where T : Behavior, new();

    /// <summary>
    /// Gets the existing trait of type <typeparamref name="T"/>, 
    /// or creates and adds a new one if absent.
    /// </summary>
    T GetOrAddTrait<T>() where T : Trait, new();
}

/// <summary>
/// An entity that can be resolved by a container-local name.
/// </summary>
public interface INamedEntity : IEntity
{
    /// <summary>
    /// Optional container-local name.
    /// </summary>
    string? Name { get; }
}
