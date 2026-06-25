namespace Blitter.Blocks;

/// <summary>
/// A collection of <see cref="Trait"/>'s and <see cref="Behavior"/>'s.
/// </summary>
public class Entity : IEntity
{
    /// <summary>
    /// The container this entity belongs, or null if this is the root.
    /// </summary>
    public IContainerEntity? Parent 
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