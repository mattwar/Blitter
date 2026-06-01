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
        if (contact.Normal.Y > GroundNormalY)
            _lastGroundedAt = _elapsed;
    }
}
