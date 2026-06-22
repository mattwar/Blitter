using System.Diagnostics.CodeAnalysis;

namespace Blitter.Blocks;

/// <summary>
/// Convenience lookups over the <see cref="IEntity"/> primitives. Extension
/// methods so they're callable on any implementer (including concrete types)
/// without a cast and without per-type duplication.
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    /// Finds the trait of type <typeparamref name="T"/>, returning <c>false</c>
    /// (and <c>null</c>) if the entity has none.
    /// </summary>
    public static bool TryGetTrait<T>(this IEntity entity, [NotNullWhen(true)] out T? trait) where T : Trait
    {
        for (int i = 0; i < entity.Traits.Count; i++)
        {
            if (entity.Traits[i] is T match)
            {
                trait = match;
                return true;
            }
        }

        trait = null;
        return false;
    }

    /// <summary>
    /// Finds a trait of type <typeparamref name="T"/> on <paramref name="entity"/>,
    /// or failing that, the nearest ancestor that has one.
    /// </summary>
    public static bool TryFindTrait<T>(this IEntity entity, [NotNullWhen(true)] out T? trait)
        where T : Trait
    {
        for (IEntity? current = entity; current is not null; current = current.Parent)
        {
            if (current.TryGetTrait(out trait))
                return true;
        }

        trait = null;
        return false;
    }

    /// <summary>
    /// Returns the trait of type <typeparamref name="T"/>, throwing if the entity
    /// has none. Use for traits a behavior requires to already exist (e.g. an
    /// intrinsic transform the user owns).
    /// </summary>
    public static T GetTrait<T>(this IEntity entity) where T : Trait =>
        entity.TryGetTrait<T>(out var trait)
            ? trait
            : throw new InvalidOperationException(
                $"Entity has no trait of type {typeof(T).Name}.");


    /// <summary>
    /// Finds the behavior of type <typeparamref name="T"/>, returning
    /// <c>false</c> (and <c>null</c>) if the entity has none.
    /// </summary>
    public static bool TryGetBehavior<T>(this IEntity entity, [NotNullWhen(true)] out T? behavior) where T : Behavior
    {
        for (int i = 0; i < entity.Behaviors.Count; i++)
        {
            if (entity.Behaviors[i] is T match)
            {
                behavior = match;
                return true;
            }
        }

        behavior = null;
        return false;
    }

    /// <summary>
    /// Returns the behavior of type <typeparamref name="T"/>, throwing if the
    /// entity has none.
    /// </summary>
    public static T GetBehavior<T>(this IEntity entity) where T : Behavior =>
        entity.TryGetBehavior<T>(out var behavior)
            ? behavior
            : throw new InvalidOperationException(
                $"Entity has no behavior of type {typeof(T).Name}.");
}
