using System.Numerics;

namespace Blitter.Blocks2D;

/// <summary>
/// Rate-of-change of an entity's placement: linear motion expressed in the
/// friendly polar form (<see cref="Speed"/> along a <see cref="Heading"/>)
/// plus angular <see cref="RotationSpeed"/>. The canonical velocity behind
/// <see cref="Sprite2D"/>'s accessors.
/// </summary>
public sealed class Velocity2D : Trait
{
    /// <summary>The direction of movement in degrees (0 = up).</summary>
    public float Heading { get; set; }

    /// <summary>Speed in world units per second along the <see cref="Heading"/>.</summary>
    public float Speed { get; set; }

    /// <summary>How many degrees the entity rotates in a second.</summary>
    public float RotationSpeed { get; set; }


    public Vector2 Vector
    {
        get 
        {
            double headingRads = (this.Heading - 90f) * (Math.PI / 180.0);
            var velocityX = this.Speed * (float)Math.Cos(headingRads);
            if (MathF.Abs(velocityX) < 0.0001f)
                velocityX = 0f;
            var velocityY = this.Speed * (float)Math.Sin(headingRads);
            if (MathF.Abs(velocityY) < 0.0001f)
                velocityY = 0f;
            return new Vector2(velocityX, velocityY);
        }

        set 
        {
            this.Speed = value.Length();
            if (this.Speed > 0f)
            {
                var headingRads = Math.Atan2(value.Y, value.X);
                this.Heading = (float)(headingRads * (180.0 / Math.PI)) + 90f;
            }
        }
    }
}
