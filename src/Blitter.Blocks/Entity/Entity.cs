namespace Blitter.Blocks;

/// <summary>
/// A collection of <see cref="Trait"/>'s and <see cref="Behavior"/>'s.
/// </summary>
public class Entity : IEntity
{
    /// <summary>
    /// The container this entity belongs, or null if this is the root.
    /// </summary>
    public IContainer? Container 
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
    /// Called when this entity is attached to a container entity.
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

    private void AddTraitCore(Trait trait)
    {
        _traits.Add(trait);
    }

    private void AddBehaviorCore(Behavior behavior)
    {
        _behaviors.Add(behavior);
        behavior.Entity = this;
    }

    /// <summary>
    /// Returns the existing behavior of type <typeparamref name="T"/>, or creates,
    /// attaches, and returns a new one if absent.
    /// </summary>
    public T GetOrAddBehavior<T>() where T : Behavior, new()
    {
        for (int i = 0; i < _behaviors.Count; i++)
        {
            if (_behaviors[i].GetType() == typeof(T))
                return (T)_behaviors[i];
        }

        var newBehavior = new T();
        AddBehaviorCore(newBehavior);
        return newBehavior;
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
        AddTraitCore(newTrait);
        return newTrait;
    }

    /// <summary>
    /// Searches this entity and its ancestors for a trait by type/>.
    /// </summary>
    public bool TryGetAncestor<TEntity>(out TEntity? ancestor) where TEntity : class, IEntity
    {
        var current = this.Container;
        while (current is not null)
        {
            if (current is TEntity match)
            {
                ancestor = match;
                return true;
            }
            current = current.Container;
        }
        ancestor = null;
        return false;
    }

}