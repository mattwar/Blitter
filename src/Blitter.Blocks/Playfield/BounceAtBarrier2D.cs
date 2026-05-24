using System.Numerics;

namespace Blitter.Blocks;

/// <summary>
/// On contact with a barrier, snaps the sprite out of penetration along
/// the contact normal and reflects velocity scaled by
/// <see cref="Restitution"/>. The pinball counterpart to
/// <see cref="StopAtBarrier2D"/>. Handles <see cref="LineBarrier2D"/>
/// and <see cref="CircleBarrier2D"/>.
/// </summary>
public sealed class BounceAtBarrier2D : SpriteBehavior2D
{
    /// <summary>Normal-component velocity scale after reflection. 1 = perfectly elastic, 0 = no bounce (matches <see cref="StopAtBarrier2D"/>).</summary>
    public float Restitution { get; set; } = 1f;

    /// <summary>Tangent-component velocity scale after reflection. Below 1 simulates surface friction.</summary>
    public float TangentialDamping { get; set; } = 1f;

    /// <summary>Called after a successful bounce. Args: sprite, barrier, contact normal.</summary>
    public Action<Sprite2D, Barrier2D, Vector2>? OnBounce { get; set; }

    public override void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext2D context)
    {
        if (!TryGetContact(self, barrier, out var normal, out var penetration))
            return;

        if (penetration > 0f)
            self.Center += normal * penetration;

        // Moving barriers (flippers, etc.) contribute their surface
        // velocity to the bounce so they actually kick the ball
        // instead of just elastically reflecting it. Stationary
        // barriers report zero, so this collapses to the
        // textbook reflection.
        var vSurface = barrier is FlipperBarrier2D flipper
            ? flipper.SurfaceVelocityAt(self.Center - normal * self.HitCircle.Radius)
            : Vector2.Zero;

        var vBall = Sprite2D.GetVelocity(self.Speed, self.Heading);
        var vRel = vBall - vSurface;
        var along = Vector2.Dot(vRel, normal);
        if (along < 0f)
        {
            var vN = normal * along;
            var vT = vRel - vN;
            vRel = vT * TangentialDamping - vN * Restitution;
            vBall = vRel + vSurface;
            (self.Speed, self.Heading) = Sprite2D.GetSpeedAndHeading(vBall);
        }

        OnBounce?.Invoke(self, barrier, normal);
    }

    private static bool TryGetContact(Sprite2D self, Barrier2D barrier, out Vector2 normal, out float penetration)
    {
        var center = self.Center;
        var radius = self.HitCircle.Radius;

        switch (barrier)
        {
            case LineBarrier2D line:
            {
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
                var delta = center - closest;
                var dist = MathF.Sqrt(Vector2.Dot(delta, delta));
                normal = line.Normal;
                penetration = radius - dist;
                return true;
            }
            case CircleBarrier2D disc:
            {
                var delta = center - disc.Center;
                var distSq = Vector2.Dot(delta, delta);
                if (distSq <= float.Epsilon)
                {
                    // Concentric — pick an arbitrary outward normal.
                    normal = new Vector2(0f, -1f);
                    penetration = disc.Radius + radius;
                    return true;
                }
                var dist = MathF.Sqrt(distSq);
                normal = delta / dist;
                penetration = (disc.Radius + radius) - dist;
                return true;
            }
            case FlipperBarrier2D flipper:
            {
                // Capsule = closest-point-on-segment + fat radius. Same
                // math as the line case but with the flipper's capsule
                // radius folded into the combined collision radius.
                var (closest, _) = FlipperBarrier2D.ClosestPointOnSegment(
                    flipper.Pivot, flipper.Tip, center);
                var delta = center - closest;
                var distSq = Vector2.Dot(delta, delta);
                var combined = radius + flipper.Radius;
                if (distSq <= float.Epsilon)
                {
                    // Ball center sits exactly on the segment — push
                    // out perpendicular to the flipper.
                    var d = flipper.Tip - flipper.Pivot;
                    var dLen = MathF.Sqrt(Vector2.Dot(d, d));
                    normal = dLen > float.Epsilon
                        ? new Vector2(-d.Y, d.X) / dLen
                        : new Vector2(0f, -1f);
                    penetration = combined;
                    return true;
                }
                var dist = MathF.Sqrt(distSq);
                normal = delta / dist;
                penetration = combined - dist;
                return true;
            }
            default:
                normal = default;
                penetration = 0f;
                return false;
        }
    }
}
