namespace Blitter.Blocks2D;

/// <summary>
/// Raised after a sprite has wrapped via <see cref="WrapInBounds2D"/>.
/// </summary>
/// <param name="Source">The behavior instance that raised the event.</param>
/// <param name="Sprite">The sprite that wrapped this tick.</param>
public readonly record struct Wrapped2DEventArgs(WrapInBounds2D Source, Sprite2D Sprite);

/// <summary>
/// Moves a sprite to the opposite edge of the update context bounds
/// when its center crosses an edge — 
/// the classic Asteroids-style toroidal world.
/// </summary>
public class WrapInBounds2D : Behavior, IUpdatable
{
    /// <summary>Optional handler invoked after the sprite wraps.</summary>
    public IEventHandler<Wrapped2DEventArgs>? Wrapped { get; set; }

    private IEntity _entity = null!;
    private Bounds2D? _bounds;
    private Transform2D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _entity = entity;
        _transform = entity.GetOrAddTrait<Transform2D>();
    }

    public void Update(in EntityUpdateContext context)
    {
        // Bounds live on an ancestor (the playfield), which isn't reachable
        // when this behavior is attached (the sprite may not be parented yet). 
        // Resolve it opportunistically once the entity is parented.
        _bounds ??= _entity.TryFindTrait<Bounds2D>(out var bounds) ? bounds : null;
        if (_bounds is null)
            return;

        if (_bounds.Rect is not Rect b)
            return;

        if (b.Width <= 0f || b.Height <= 0f)
            return;

        var c = _transform.Position;
        var wrapped = false;

        if (c.X < b.X)                  
        { 
            c.X += b.Width;  
            wrapped = true; 
        }
        else if (c.X > b.X + b.Width)
        { 
            c.X -= b.Width;  
            wrapped = true; 
        }

        if (c.Y < b.Y)                  
        { 
            c.Y += b.Height; 
            wrapped = true; 
        }
        else if (c.Y > b.Y + b.Height)
        { 
            c.Y -= b.Height; 
            wrapped = true; 
        }

        if (wrapped)
        {
            _transform.Position = c;
            if (_entity is Sprite2D target)
            {
                var args = new Wrapped2DEventArgs(this, target);
                Wrapped?.OnEvent(in args);
            }
        }
    }
}
