namespace Blitter.Blocks2D;

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
        var v = Sprite2D.GetVelocity(target.Speed, target.Heading);
        var bounced = false;

        if (target.Center.X < bounds.X)
        {
            v.X = MathF.Abs(v.X);
            bounced = true;
        }
        else if (target.Center.X > bounds.X + bounds.Width)
        {
            v.X = -MathF.Abs(v.X);
            bounced = true;
        }

        if (target.Center.Y < bounds.Y)
        {
            v.Y = MathF.Abs(v.Y);
            bounced = true;
        }
        else if (target.Center.Y > bounds.Y + bounds.Height)
        {
            v.Y = -MathF.Abs(v.Y);
            bounced = true;
        }

        if (!bounced)
            return;

        (target.Speed, target.Heading) = Sprite2D.GetSpeedAndHeading(v);
        OnBounce?.Invoke(target);
    }
}
