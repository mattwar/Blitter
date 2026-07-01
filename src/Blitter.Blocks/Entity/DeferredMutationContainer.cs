using System.Diagnostics.CodeAnalysis;

namespace Blitter.Blocks;

/// <summary>
/// List-backed container that buffers membership changes while an external
/// operation traverses its entities.
/// </summary>
public class DeferredMutationContainer : Entity, IDeferredMutationContainer
{
    private readonly List<IEntity> _entities = new();
    private readonly List<IEntity> _pendingAddEntities = new();
    private readonly HashSet<IEntity> _pendingRemoveEntities = new(ReferenceEqualityComparer.Instance);
    private int _mutationBufferDepth;

    /// <summary>
    /// The entities currently in this container. The <c>init</c> accessor
    /// adopts an initial set at construction.
    /// </summary>
    public IReadOnlyList<IEntity> Entities
    {
        get => _entities;
        init => AdoptEntities(value);
    }

    /// <summary>
    /// Tries to resolve the single entity assignable to <typeparamref name="T"/>
    /// in this container. Returns <c>false</c> if none. Throws if more than one
    /// matches.
    /// </summary>
    public bool TryGetEntity<T>([NotNullWhen(true)] out T? entity) where T : class, IEntity
    {
        T? match = null;
        foreach (var candidate in _entities)
        {
            if (candidate is not T typed)
                continue;
            if (match is not null)
                throw new InvalidOperationException($"More than one entity is a {typeof(T).Name}.");
            match = typed;
        }
        entity = match;
        return match is not null;
    }

    /// <summary>
    /// Resolves the single entity assignable to <typeparamref name="T"/> in
    /// this container. Throws if none exists or more than one matches.
    /// </summary>
    public T GetEntity<T>() where T : class, IEntity =>
        TryGetEntity<T>(out var entity) ? entity : throw new InvalidOperationException($"No entity of type {typeof(T).Name}.");

    /// <inheritdoc/>
    public void AddEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var existing = entity.Container as DeferredMutationContainer;
        if (existing is not null && existing != this)
            existing.RemoveImmediate(entity);
        else if (existing == this)
        {
            _pendingRemoveEntities.Remove(entity);
            if (IsEntityMember(entity))
                return;
        }
        else
        {
            entity.Container?.RemoveEntity(entity);
        }

        if (entity.Container != this)
            SetContainer(entity, this);

        if (IsBufferingMutations)
            _pendingAddEntities.Add(entity);
        else
            _entities.Add(entity);
    }

    /// <inheritdoc/>
    public void RemoveEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Container != this && !IsEntityMember(entity))
            return;

        if (IsBufferingMutations)
        {
            var removedPendingAdd = _pendingAddEntities.Remove(entity);
            if (_entities.Contains(entity))
                _pendingRemoveEntities.Add(entity);
            else if (removedPendingAdd)
                Detach(entity);
        }
        else if (_entities.Remove(entity))
        {
            Detach(entity);
        }
    }

    /// <inheritdoc/>
    public Containment GetContainment(IEntity entity)
    {
        if (_pendingRemoveEntities.Contains(entity))
            return Containment.Removing;

        if (IsEntityMember(entity))
        {
            if (!ReferenceEquals(entity.Container, this))
                return Containment.NotContained;
            return Containment.Contained;
        }

        return Containment.NotContained;
    }

    /// <inheritdoc/>
    public bool Contains(IEntity entity) =>
        GetContainment(entity) == Containment.Contained;

    /// <inheritdoc/>
    public void BeginMutationBuffer()
    {
        _mutationBufferDepth++;
    }

    /// <inheritdoc/>
    public void EndMutationBuffer()
    {
        if (_mutationBufferDepth <= 0)
            throw new InvalidOperationException($"{nameof(EndMutationBuffer)} called without a matching {nameof(BeginMutationBuffer)}.");

        _mutationBufferDepth--;
        if (_mutationBufferDepth == 0)
            ApplyPendingChanges();
    }

    private bool IsBufferingMutations => _mutationBufferDepth > 0;

    private bool IsEntityMember(IEntity entity) =>
        _entities.Contains(entity) || _pendingAddEntities.Contains(entity);

    private void AdoptEntities(IEnumerable<IEntity> entities)
    {
        foreach (var entity in entities)
        {
            (entity.Container as DeferredMutationContainer)?.RemoveImmediate(entity);
            SetContainer(entity, this);
            _entities.Add(entity);
        }
    }

    private void RemoveImmediate(IEntity entity)
    {
        var removed = _pendingAddEntities.Remove(entity);
        _pendingRemoveEntities.Remove(entity);
        if (_entities.Remove(entity))
            removed = true;
        if (removed)
            Detach(entity);
    }

    private void Detach(IEntity entity)
    {
        if (entity.Container == this)
            SetContainer(entity, null);
    }

    private static void SetContainer(IEntity child, IContainer? container)
    {
        if (child is Entity entity)
        {
            entity.Container = container;
            return;
        }

        throw new InvalidOperationException($"{nameof(DeferredMutationContainer)} can only contain {nameof(Entity)} instances.");
    }

    private void ApplyPendingChanges()
    {
        if (_pendingRemoveEntities.Count > 0)
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                var entity = _entities[i];
                if (!_pendingRemoveEntities.Contains(entity)) continue;
                _entities.RemoveAt(i);
                Detach(entity);
            }
            _pendingRemoveEntities.Clear();
        }

        if (_pendingAddEntities.Count > 0)
        {
            _entities.AddRange(_pendingAddEntities);
            _pendingAddEntities.Clear();
        }
    }
}
