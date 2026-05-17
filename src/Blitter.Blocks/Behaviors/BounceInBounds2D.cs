namespace Blitter.Blocks;

/// <summary>
/// Reflects a sprite's velocity when its center crosses the edge of the
/// update context bounds, so the sprite stays inside the playfield.
/// </summary>
public class BounceInBounds2D : SpriteBehavior2D
{
    /// <summary>
    /// Invoked after the velocity has been reflected for the current tick.
    /// Useful for playing a sound or jittering the heading on bounce.
    /// </summary>
    public Action<Sprite2D>? OnBounce { get; set; }

    public override void Update(Sprite2D target, in UpdateContext2D context)
    {
        var bounds = context.Bounds;
        var bounced = false;

        if (target.CenterX < bounds.X)
        {
            target.ChangeVelocity((vx, vy) => (Math.Abs(vx), vy));
            bounced = true;
        }
        else if (target.CenterX > bounds.X + bounds.Width)
        {
            target.ChangeVelocity((vx, vy) => (-Math.Abs(vx), vy));
            bounced = true;
        }

        if (target.CenterY < bounds.Y)
        {
            target.ChangeVelocity((vx, vy) => (vx, Math.Abs(vy)));
            bounced = true;
        }
        else if (target.CenterY > bounds.Y + bounds.Height)
        {
            target.ChangeVelocity((vx, vy) => (vx, -Math.Abs(vy)));
            bounced = true;
        }

        if (bounced)
            OnBounce?.Invoke(target);
    }
}
