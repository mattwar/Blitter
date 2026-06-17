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
    /// Gets the first existing behavior of type <typeparamref name="T"/>, 
    /// or creates and adds one if absent.
    /// </summary>
    T GetOrAddBehavior<T>() where T : Behavior, new();

    /// <summary>
    /// Advance this entity one tick.
    /// </summary>
    void Update(in UpdateContext context);
}
