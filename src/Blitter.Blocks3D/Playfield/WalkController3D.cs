using System.Numerics;

namespace Blitter.Blocks3D;

/// <summary>
/// First-person walk controller for a <see cref="Sprite3D"/>. Drives
/// the host sprite's horizontal <see cref="Sprite3D.Velocity"/> from
/// keyboard input, fires a vertical jump impulse on a key press while
/// recently grounded, and (optionally) slaves a <see cref="Camera3D"/>
/// to the sprite's eye every frame. The vertical velocity channel is
/// left to other behaviors such as <see cref="Gravity3D"/> and
/// <see cref="BarrierBounce3D"/>.
/// </summary>
/// <remarks>
/// Pair with <c>RelativeMouseMode = true</c> on the source
/// <see cref="Window"/> so <see cref="FrameInput.MouseDelta"/>
/// reports unclamped per-frame motion in pixels.
/// </remarks>
public class WalkController3D : SpriteBehavior3D
{
    private readonly Window _window;
    private TimeSpan _elapsed;
    private TimeSpan? _lastGroundedAt;
    // Wall normals recorded from OnHitBarrier during the most recent
    // collision pass. Consumed on the next Apply to clip requested
    // motion against each wall independently, so corners (two walls
    // hit in the same frame) stop the player cleanly instead of
    // letting one wall's projection push into the other.
    private readonly Vector3[] _wallNormals = new Vector3[4];
    private int _wallCount;

    public WalkController3D(Window window, Camera3D? camera = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        Camera = camera;
    }

    /// <summary>Camera follow target. When set, the controller writes <see cref="Camera3D.Position"/>, <see cref="Camera3D.Target"/>, and <see cref="Camera3D.Up"/> each frame.</summary>
    public Camera3D? Camera { get; set; }

    /// <summary>Yaw around world +Y in radians. Increases when looking left.</summary>
    public float Yaw { get; set; }

    /// <summary>Pitch in radians, clamped to <see cref="MaxPitch"/>.</summary>
    public float Pitch { get; set; }

    /// <summary>Maximum absolute pitch in radians. Defaults to just under 90°.</summary>
    public float MaxPitch { get; set; } = MathF.PI / 2f - 0.05f;

    /// <summary>Horizontal walk speed in world units per second.</summary>
    public float MoveSpeed { get; set; } = 4.5f;

    /// <summary>Multiplier applied to <see cref="MoveSpeed"/> while a sprint key is held.</summary>
    public float SprintMultiplier { get; set; } = 1.8f;

    /// <summary>Initial vertical speed of a jump in world units per second.</summary>
    public float JumpSpeed { get; set; } = 6.0f;

    /// <summary>World-space Y offset from the sprite's position to the camera eye.</summary>
    public float EyeOffsetY { get; set; } = 0.6f;

    /// <summary>Mouse-look sensitivity in radians per pixel of <see cref="FrameInput.MouseDelta"/>.</summary>
    public float LookSpeed { get; set; } = 0.005f;

    /// <summary>Window after a ground contact during which a jump key press still counts as a jump. Covers single-frame lift-offs and forgives uneven terrain.</summary>
    public TimeSpan CoyoteWindow { get; set; } = TimeSpan.FromMilliseconds(120);

    /// <summary>Minimum upward Y component of a contact normal for the contact to count as ground.</summary>
    public float GroundNormalY { get; set; } = 0.5f;

    /// <summary>True while the controller is within its <see cref="CoyoteWindow"/> of a ground contact.</summary>
    public bool IsGrounded =>
        _lastGroundedAt is { } at && (_elapsed - at) <= CoyoteWindow;

    /// <summary>World-space eye position used for the camera follow.</summary>
    public Vector3 Eye { get; private set; }

    /// <summary>World-space unit forward vector derived from <see cref="Yaw"/> and <see cref="Pitch"/>.</summary>
    public Vector3 LookDirection { get; private set; } = -Vector3.UnitZ;

    // ---- Key bindings -----------------------------------------------------

    public Key ForwardKey  { get; set; } = Key.W;
    public Key BackwardKey { get; set; } = Key.S;
    public Key LeftKey     { get; set; } = Key.A;
    public Key RightKey    { get; set; } = Key.D;
    public Key JumpKey     { get; set; } = Key.Space;
    public Key SprintKey    { get; set; } = Key.LShift;
    public Key SprintAltKey { get; set; } = Key.RShift;

    public override void Apply(Sprite3D target, in UpdateContext3D context)
    {
        _elapsed += context.ElapsedSinceLastUpdate;
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var input = _window.Input;

        // Mouse look.
        var delta = input.MouseDelta;
        if (delta != Vector2.Zero)
        {
            Yaw -= delta.X * LookSpeed;
            Pitch = Math.Clamp(Pitch - delta.Y * LookSpeed, -MaxPitch, MaxPitch);
        }

        // Horizontal movement basis: yaw-only, so looking down doesn't shorten the step.
        var forwardFlat = new Vector3(-MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw));
        var rightFlat = Vector3.Normalize(Vector3.Cross(forwardFlat, Vector3.UnitY));

        var move = Vector3.Zero;
        if (input.IsDown(ForwardKey))  move += forwardFlat;
        if (input.IsDown(BackwardKey)) move -= forwardFlat;
        if (input.IsDown(RightKey))    move += rightFlat;
        if (input.IsDown(LeftKey))     move -= rightFlat;

        float speed = MoveSpeed;
        if (input.IsDown(SprintKey) || input.IsDown(SprintAltKey))
            speed *= SprintMultiplier;

        var horiz = move == Vector3.Zero
            ? Vector3.Zero
            : Vector3.Normalize(move) * speed;

        // Clip the requested motion against every wall recorded last
        // collision pass. Two iterations are enough to settle a corner:
        // the first removes the inward component of one wall, which
        // may reintroduce inward motion into another, and the second
        // removes that. Without this, summing corner normals into a
        // single 45° plane would still let motion slide into a wall.
        if (_wallCount > 0)
        {
            for (int iter = 0; iter < 2; iter++)
            {
                bool clipped = false;
                for (int i = 0; i < _wallCount; i++)
                {
                    var n = _wallNormals[i];
                    var into = Vector3.Dot(horiz, n);
                    if (into < 0f)
                    {
                        horiz -= n * into;
                        clipped = true;
                    }
                }
                if (!clipped)
                    break;
            }
            _wallCount = 0;
        }

        var v = target.Velocity;
        v.X = horiz.X;
        v.Z = horiz.Z;

        // Jump: edge-triggered, gated by coyote window.
        if (input.WasJustPressed(JumpKey)
            && _lastGroundedAt is { } groundedAt
            && (_elapsed - groundedAt) <= CoyoteWindow)
        {
            v.Y = JumpSpeed;
            _lastGroundedAt = null;   // consume
        }
        target.Velocity = v;

        // Look vector + camera follow.
        var cosP = MathF.Cos(Pitch);
        LookDirection = new Vector3(
            -cosP * MathF.Sin(Yaw),
             MathF.Sin(Pitch),
            -cosP * MathF.Cos(Yaw));
        Eye = target.Position + new Vector3(0f, EyeOffsetY, 0f);

        if (Camera is { } camera)
        {
            camera.Position = Eye;
            camera.Target = Eye + LookDirection;
            camera.Up = Vector3.UnitY;
        }
    }

    public override void OnHitBarrier(Sprite3D self, Barrier3D barrier, in UpdateContext3D context)
    {
        if (!self.HitShape.TryGetContact(barrier.HitShape, out var contact))
            return;
        // Normal convention: contact.Normal points from the barrier
        // surface toward the sprite. A sufficiently upward normal means
        // we're on ground (not bumping a near-vertical wall).
        var n = contact.Normal;
        if (n.Y > GroundNormalY)
        {
            _lastGroundedAt = _elapsed;
            return;
        }

        // Wall contact: remember the horizontal component so the next
        // Apply can project requested motion tangent to it. If multiple
        // walls are hit in one step (corner), accumulate so we don't
        // squeeze through the seam.
        var horizN = new Vector3(n.X, 0f, n.Z);
        var len2 = horizN.LengthSquared();
        if (len2 <= 1e-6f)
            return;
        horizN /= MathF.Sqrt(len2);

        // De-dupe near-parallel normals (multiple voxel cells of the
        // same wall report the same axis-aligned normal) and cap the
        // buffer.
        for (int i = 0; i < _wallCount; i++)
        {
            if (Vector3.Dot(_wallNormals[i], horizN) > 0.99f)
                return;
        }
        if (_wallCount < _wallNormals.Length)
            _wallNormals[_wallCount++] = horizN;
    }
}
