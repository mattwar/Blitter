namespace Blitter.Blocks;

/// <summary>
/// Moves a sprite to the opposite edge of the update context bounds
/// when its center crosses an edge — 
/// the classic Asteroids-style toroidal world.
/// </summary>
public class WrapInBounds2D : SpriteBehavior2D
{
    /// <summary>Invoked after the sprite has wrapped this tick.</summary>
    public Action<Sprite2D>? OnWrap { get; set; }

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        var b = context.Bounds;
        if (b.Width <= 0f || b.Height <= 0f)
            return;

        var c = target.Center;
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
            target.Center = c;
            OnWrap?.Invoke(target);
        }
    }
}
