namespace Blitter.Blocks;

/// <summary>
/// Default list-backed implementation of <see cref="IContainerEntity"/>.
/// </summary>
public class ContainerEntity : Entity, IContainerEntity
{
    private readonly List<IEntity> _entities = new();

    /// <inheritdoc/>
    public IReadOnlyList<IEntity> Entities
    {
        get => _entities;
        init
        {
            foreach (var entity in _entities)
            {
                if (entity is Entity child && child.Container == this)
                    child.Container = null;
            }

            _entities.Clear();
            foreach (var entity in value)
                AddEntityCore(entity);
        }
    }

    /// <inheritdoc/>
    public void AddEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (_entities.Contains(entity))
            return;

        entity.Container?.RemoveEntity(entity);
        AddEntityCore(entity);
    }

    /// <inheritdoc/>
    public void RemoveEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!_entities.Remove(entity))
            return;

        if (entity is Entity child && child.Container == this)
            child.Container = null;
    }

    /// <inheritdoc/>
    public virtual Containment GetContainment(IEntity entity) =>
        _entities.Contains(entity) ? Containment.Contained : Containment.NotContained;

    /// <inheritdoc/>
    public bool Contains(IEntity entity) =>
        GetContainment(entity) == Containment.Contained;

    private void AddEntityCore(IEntity entity)
    {
        _entities.Add(entity);
        if (entity is Entity child)
            child.Container = this;
    }
}