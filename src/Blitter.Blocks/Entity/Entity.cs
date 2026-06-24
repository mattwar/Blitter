namespace Blitter.Blocks;

/// <summary>
/// A collection of <see cref="Trait"/>'s and <see cref="Behavior"/>'s.
/// </summary>
public class Entity : IEntity
{
    /// <summary>
    /// The container this entity belongs, or null if this is the root.
    /// </summary>
    public IEntity? Parent 
    { 
        get; 
        set
        {
            field = value;
            if (value is {} entity)
                OnAttach(entity);
        }
    }

    /// <summary>
    /// Called when this entities is attached to a parent entity.
    /// </summary>
    protected virtual void OnAttach(IEntity entity)
    {    
        // initialize behaviors that were assigned before attach
        foreach (var behavior in this.Behaviors)
        {
            behavior.Entity = this;
        }
    }

    private readonly List<Trait> _traits = new();
    private readonly List<Behavior> _behaviors = new();

    /// <summary>
    /// The traits attached to this entity.
    /// </summary>
    public IReadOnlyList<Trait> Traits 
    { 
        get => _traits; 

        init
        {
            if (_traits.Count > 0)
            {
                // we may have a collision during initialization.
                foreach (var trait in value)
                {
                    int i = _traits.FindIndex(existing => existing.GetType() == trait.GetType());
                    if (i >= 0)
                    {
                        // let new value win
                        _traits[i] = trait;
                    }
                    else
                    {
                        _traits.Add(trait);
                    }
                }
            }
            else
            {
                _traits.AddRange(value);
            }
        }   
    }

    /// <summary>
    /// The behaviors attached to this entity.
    /// </summary>
    public IReadOnlyList<Behavior> Behaviors { 
        get => _behaviors; 
        init
        {
            if (_behaviors.Count > 0)
            {
                // we may have a collision during initialization.
                foreach (var behavior in value)
                {
                    int i = _behaviors.FindIndex(existing => existing.GetType() == behavior.GetType());
                    if (i >= 0)
                    {
                        // let new value win
                        _behaviors[i] = behavior;
                        behavior.Entity = this;
                    }
                    else
                    {
                        _behaviors.Add(behavior);
                        behavior.Entity = this;
                    }
                }
            }
            else 
            {
                foreach (var behavior in value)
                {
                    _behaviors.Add(behavior);
                    behavior.Entity = this;
                }
            }
        }   
    }

    /// <summary>
    /// Adds a trait to this entity.
    /// </summary>
    public void AddTrait(Trait trait)
    {
        _traits.Add(trait);
    }

    /// <summary>
    /// Adds a behavior to this entity.
    /// </summary>
    public void AddBehavior(Behavior behavior)
    {
        _behaviors.Add(behavior);
        behavior.Entity = this;
    }

    /// <summary>
    /// Returns the existing trait of type <typeparamref name="T"/>, or creates,
    /// attaches, and returns a new one if absent. Use for capability traits a
    /// behavior summons (e.g. velocity) where the default value is correct.
    /// </summary>
    public T GetOrAddTrait<T>() where T : Trait, new() 
    {
        if (this.TryGetTrait<T>(out var existing))
            return existing;            
        var newTrait = new T();
        AddTrait(newTrait);
        return newTrait;
    }

    /// <summary>
    /// Searches this entity and its ancestors for a trait by type/>.
    /// </summary>
    public bool TryGetAncestor<TEntity>(out TEntity? ancestor) where TEntity : class, IEntity
    {
        var current = this.Parent;
        while (current is not null)
        {
            if (current is TEntity match)
            {
                ancestor = match;
                return true;
            }
            current = current.Parent;
        }
        ancestor = null;
        return false;
    }

    /// <summary>
    /// Reports the membership state of <paramref name="child"/> within this entity. 
    /// </summary>
    public virtual Containment GetContainment(IEntity child) => 
        Containment.NotContained;

    /// <summary>
    /// Determines whether this entity contains the specified <paramref name="child"/>.
    /// </summary>
    public bool Contains(IEntity child) => 
        GetContainment(child) == Containment.Contained;

    /// <summary>
    /// Advances the entity one tick by applying each attached behavior in order.
    /// </summary>
    public virtual void Update(in UpdateContext context)
    {
        for (int i = 0; i < this.Behaviors.Count; i++)
        {
            if (this.Behaviors[i] is IUpdatable updatable)
                updatable.Update(in context);
        }
    }
}


#if false
public class EntityContainer : Entity
{
    private readonly List<IEntity> _children = new();

    /// <summary>
    /// The entities contained by this entity.
    /// </summary>
    public IReadOnlyList<IEntity> Children
    {
        get => _children;

        init
        {
            _children.AddRange(value);
            foreach (var entity in value)
            {
                if (entity is Entity e)
                {
                    e.Parent = this;
                }
            }
        }
    }

    /// <summary>
    /// Adds a child to this entity.
    /// </summary>
    public void AddChild(IEntity child)
    {
        _children.Add(child);
        if (child is Entity e)
        {
            e.Parent = this;
        }
    }

    /// <inheritdoc/>
    public override Containment GetContainment(IEntity child) =>
        _children.Contains(child)
            ? Containment.Contained
            : Containment.NotContained;
}
#endif