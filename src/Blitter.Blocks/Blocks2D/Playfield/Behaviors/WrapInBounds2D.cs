using Blitter.Bits;

namespace Blitter.Blocks2D;

/// <summary>
/// Moves a sprite to the opposite edge of the update context bounds
/// when its center crosses an edge — 
/// the classic Asteroids-style toroidal world.
/// </summary>
public class WrapInBounds2D : Behavior
{
    /// <summary>Invoked after the sprite has wrapped this tick.</summary>
    public Action<Sprite2D>? OnWrap { get; set; }

    private IEntity _entity = null!;
    private Bounds2D? _bounds;
    private Transform2D _transform = null!;

    protected override void OnAttach(IEntity entity)
    {
        _entity = entity;
        _transform = entity.GetOrAddTrait<Transform2D>();
    }

    public override void Apply(in UpdateContext context)
    {
        // Bounds live on an ancestor (the playfield), which isn't reachable
        // when this behavior is attached (the sprite may not be parented yet). 
        // Resolve it opportunistically once the entity is parented.
        _bounds ??= _entity.TryFindTrait<Bounds2D>(out var bounds) ? bounds : null;
        if (_bounds is null)
            return;

        var b = _bounds.Rect;
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
                OnWrap?.Invoke(target);
        }
    }
}
