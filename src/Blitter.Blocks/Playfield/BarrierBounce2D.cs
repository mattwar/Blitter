using System.Numerics;

namespace Blitter.Blocks;

/// <summary>
/// On contact with a barrier, snaps the sprite out of penetration along
/// the contact normal and reflects velocity. Final bounce composes the
/// behavior's ball-side <see cref="Restitution"/> /
/// <see cref="TangentialDamping"/> with the barrier's
/// <see cref="Barrier2D.Material"/>. Handles <see cref="LineBarrier2D"/>,
/// <see cref="CircleBarrier2D"/>, and <see cref="SwingArmBarrier2D"/>.
/// </summary>
public sealed class BarrierBounce2D : SpriteBehavior2D
{
    /// <summary>Ball-side elastic coefficient. Multiplied with the barrier's <see cref="BarrierMaterial.Restitution"/>. 1 = perfectly elastic, 0 = sticks.</summary>
    public float Restitution { get; set; } = 1f;

    /// <summary>Ball-side tangent velocity retention. Multiplied with <c>(1 - barrier.Material.Friction)</c>. 1 = frictionless ball, &lt; 1 = ball-side surface drag.</summary>
    public float TangentialDamping { get; set; } = 1f;

    /// <summary>Called after a successful bounce. Args: sprite, barrier, contact normal.</summary>
    public Action<Sprite2D, Barrier2D, Vector2>? OnBounce { get; set; }

    public override void OnHitBarrier(Sprite2D self, Barrier2D barrier, in UpdateContext2D context)
    {
        if (!TryGetContact(self, barrier, out var normal, out var penetration))
            return;

        if (penetration > 0f)
            self.Center += normal * penetration;

        // Surface velocity at the contact point. Stationary barriers
        // report zero so this collapses to the textbook reflection;
        // moving barriers (flippers, etc.) contribute their motion.
        var contactPoint = self.Center - normal * self.HitCircle.Radius;
        var vSurface = barrier.SurfaceVelocityAt(contactPoint);
        var mat = barrier.Material;

        var vBall = Sprite2D.GetVelocity(self.Speed, self.Heading);
        var vRel = vBall - vSurface;
        var along = Vector2.Dot(vRel, normal);
        if (along < 0f)
        {
            var vN = normal * along;
            var vT = vRel - vN;
            // Compose ball-side and barrier-side material: behavior
            // values are the ball's defaults, barrier values modulate
            // them per-surface.
            var normalScale = Restitution * mat.Restitution;
            var tangentMul = TangentialDamping * (1f - mat.Friction);
            vRel = vT * tangentMul - vN * normalScale;
            vBall = vRel + vSurface;
            if (mat.KickSpeed != 0f)
                vBall += normal * mat.KickSpeed;
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
            case SwingArmBarrier2D flipper:
            {
                // Capsule = closest-point-on-segment + fat radius. Same
                // math as the line case but with the flipper's capsule
                // radius folded into the combined collision radius.
                var (closest, _) = SwingArmBarrier2D.ClosestPointOnSegment(
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
