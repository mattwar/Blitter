using System.Numerics;

namespace Blitter.Blocks;

/// <summary>
/// On contact with a <see cref="LineBarrier2D"/>, snaps the sprite out
/// of penetration along the barrier's outward normal and cancels the
/// component of velocity pointing into the surface. Tangential motion
/// (sliding along the barrier) is preserved, so this works for floors,
/// walls, and ceilings simultaneously.
/// </summary>
public sealed class BarrierStop2D : SpriteBehavior2D
{
    /// <summary>
    /// True while the sprite is resting on a floor (a barrier whose
    /// outward normal points more up than sideways). Updated each frame
    /// from contacts seen in the previous frame, so jump input can read
    /// this during <see cref="Apply"/>.
    /// </summary>
    public bool IsGrounded { get; private set; }

    // Floor contact registered by OnHitBarrier on the previous frame.
    // Apply consumes and clears it; OnHitBarrier (which runs after
    // Apply within the same frame) sets it for next frame.
    private bool _floorContactSeen;

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        IsGrounded = _floorContactSeen;
        _floorContactSeen = false;
    }

    public override void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext2D context)
    {
        if (barrier is not LineBarrier2D line)
            return;

        var normal = line.Normal;
        var center = self.Center;

        // Closest point on segment to circle center -> penetration depth.
        var ab = line.End - line.Start;
        var lenSq = Vector2.Dot(ab, ab);
        Vector2 closest;
        if (lenSq <= float.Epsilon)
        {
            closest = line.Start;
        }
        else
        {
            var t = Vector2.Dot(center - line.Start, ab) / lenSq;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            closest = line.Start + ab * t;
        }

        var radius = self.HitCircle.Radius;
        var delta = center - closest;
        var dist = MathF.Sqrt(Vector2.Dot(delta, delta));
        if (dist < radius)
        {
            // Push out along outward normal so the sprite rests on the
            // surface rather than inside it.
            self.Center = center + normal * (radius - dist);
        }

        // Zero the component of velocity heading INTO the surface.
        // Tangential motion is preserved.
        var v = Sprite2D.GetVelocity(self.Speed, self.Heading);
        var along = Vector2.Dot(v, normal);
        if (along < 0f)
        {
            v -= normal * along;
            (self.Speed, self.Heading) = Sprite2D.GetSpeedAndHeading(v);
        }

        // Floor-ish contact: normal pointing more up than sideways.
        if (normal.Y < -0.7f)
            _floorContactSeen = true;
    }
}
