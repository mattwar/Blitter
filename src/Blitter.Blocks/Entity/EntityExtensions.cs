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
        for (IEntity? current = entity; current is not null; current = current.Container)
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
    /// Removes <paramref name="entity"/> from its current
    /// <see cref="IEntity.Container"/> container, if any.
    /// </summary>
    public static void RemoveFromContainer(this IEntity entity) =>
        entity.Container?.RemoveEntity(entity);


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

    /// <summary>
    /// Finds a capability of type <typeparamref name="T"/> provided by
    /// <paramref name="entity"/> itself or by one of its behaviors.
    /// </summary>
    public static bool TryGetCapability<T>(this IEntity entity, [NotNullWhen(true)] out T? capability) where T : class
    {
        if (entity is T self)
        {
            capability = self;
            return true;
        }

        for (int i = 0; i < entity.Behaviors.Count; i++)
        {
            if (entity.Behaviors[i] is T match)
            {
                capability = match;
                return true;
            }
        }

        capability = null;
        return false;
    }

    /// <summary>
    /// Returns a capability of type <typeparamref name="T"/> provided by
    /// <paramref name="entity"/> itself or by one of its behaviors.
    /// </summary>
    public static T GetCapability<T>(this IEntity entity) where T : class =>
        entity.TryGetCapability<T>(out var capability)
            ? capability
            : throw new InvalidOperationException(
                $"Entity has no capability of type {typeof(T).Name}.");

    /// <summary>
    /// Tries to resolve the single child entity assignable to <typeparamref name="T"/>.
    /// Returns <c>false</c> if none. Throws if more than one matches.
    /// </summary>
    public static bool TryGetEntity<T>(this IContainer container, [NotNullWhen(true)] out T? entity) where T : class
    {
        T? match = null;
        foreach (var candidate in container.Entities)
        {
            if (candidate is not T typed)
                continue;
            if (match is not null)
                throw new InvalidOperationException($"More than one entity is a {typeof(T).Name}; resolve it by name instead.");
            match = typed;
        }
        entity = match;
        return match is not null;
    }

    /// <summary>
    /// Resolves the single child entity assignable to <typeparamref name="T"/>.
    /// Throws if none exists or more than one matches.
    /// </summary>
    public static T GetEntity<T>(this IContainer container) where T : class =>
        container.TryGetEntity<T>(out var entity) ? entity : throw new InvalidOperationException($"No entity of type {typeof(T).Name}.");

    /// <summary>
    /// Tries to resolve the child entity named <paramref name="name"/> as a
    /// <typeparamref name="T"/>. Returns <c>false</c> if no child has that
    /// name. Throws if the name is duplicated or the named entity is a
    /// different type.
    /// </summary>
    public static bool TryGetEntity<T>(this IContainer container, string name, [NotNullWhen(true)] out T? entity) where T : class
    {
        ArgumentNullException.ThrowIfNull(name);
        INamedEntity? named = null;
        foreach (var candidate in container.Entities)
        {
            if (candidate is not INamedEntity namedCandidate || namedCandidate.Name != name)
                continue;
            if (named is not null)
                throw new InvalidOperationException($"More than one entity is named '{name}'.");
            named = namedCandidate;
        }
        if (named is null)
        {
            entity = null;
            return false;
        }
        if (named is T typed)
        {
            entity = typed;
            return true;
        }
        throw new InvalidOperationException($"Entity '{name}' is a {named.GetType().Name}, not a {typeof(T).Name}.");
    }

    /// <summary>
    /// Resolves the child entity named <paramref name="name"/> as a
    /// <typeparamref name="T"/>. Throws if no such entity exists or it is a
    /// different type.
    /// </summary>
    public static T GetEntity<T>(this IContainer container, string name) where T : class =>
        container.TryGetEntity<T>(name, out var entity) ? entity : throw new InvalidOperationException($"No entity named '{name}'.");
}
