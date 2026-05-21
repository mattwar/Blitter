namespace Blitter.Blocks;

using System.Numerics;

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

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        var bounds = context.Bounds;
        var bounced = false;

        if (target.Center.X < bounds.X)
        {
            target.ChangeVelocity(v => new Vector2(Math.Abs(v.X), v.Y));
            bounced = true;
        }
        else if (target.Center.X > bounds.X + bounds.Width)
        {
            target.ChangeVelocity(v => new Vector2(-Math.Abs(v.X), v.Y));
            bounced = true;
        }

        if (target.Center.Y < bounds.Y)
        {
            target.ChangeVelocity(v => new Vector2(v.X, Math.Abs(v.Y)));
            bounced = true;
        }
        else if (target.Center.Y > bounds.Y + bounds.Height)
        {
            target.ChangeVelocity(v => new Vector2(v.X, -Math.Abs(v.Y)));
            bounced = true;
        }

        if (bounced)
            OnBounce?.Invoke(target);
    }
}
