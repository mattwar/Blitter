namespace Blitter.Blocks;

/// <summary>
/// A unit logic attached to an <see cref="IEntity"/>.
/// </summary>
public abstract class Behavior
{
    public IEntity Entity 
    { 
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;
            field = value;
            if (value is {} entity)
                OnAttach(entity);
        }
    } = null!;

    /// <summary>
    /// Called when the behavior is attached to an entity.
    /// </summary>
    protected virtual void OnAttach(IEntity entity)
    {
        // default does nothing 
    }

    /// <summary>
    /// Apply this behavior to its entity for one tick.
    /// </summary>
    public abstract void Apply(in UpdateContext context);
}
