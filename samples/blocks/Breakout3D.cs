#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run Breakout3D.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Breakout, in 3D, in one file:
//   * you are the paddle, looking down a closed box arena
//   * arrow keys / WASD slide you on the near plane (X / Y)
//   * Space launches the ball; it bounces off the five walls and
//     deflects off your paddle with classic position-based "english"
//   * the back of the arena is a 5x7 grid of colored bricks; each
//     one shatters on contact and adds to the score
//   * miss the ball (it sails behind you out of the arena) and you
//     lose a life. Three lives, then GAME OVER. Clear every brick
//     to WIN.
//
// Controls:
//   Arrows / WASD .............. slide paddle on the near plane
//   Space ...................... launch ball / start new game
//   Esc ........................ quit

using System.Numerics;

using Blitter;
using Blitter.Bits;
using Blitter.Blocks3D;

// Arena layout (right-handed, Y up, looking down -Z).
// Paddle sits at +Z (near you); bricks at -Z (far end).
const float ArenaHalfW = 5f;
const float ArenaHalfH = 3.5f;
const float PaddleZ   = +8f;
const float BackZ     = -10f;

const float PaddleHalfW = 1.0f;
const float PaddleHalfH = 0.75f;
const float PaddleHalfD = 0.15f;
const float PaddleSpeed = 9f;       // units per second
const float PaddleEnglish = 7f;     // tangential kick per unit of off-center hit

const float BallRadius = 0.22f;
const float BallLaunchSpeed = 9f;
const float BallMaxSpeed = 16f;

// Miss fires as soon as the ball clears the paddle's back face. There
// are no walls past the paddle, so a ball that gets behind it can't
// come back, and waiting for further overshoot just feels broken.
const float DrainZ    = PaddleZ + PaddleHalfD + BallRadius;

const int BrickCols = 7;
const int BrickRows = 5;
const float BrickW = 1.2f;
const float BrickH = 0.6f;
const float BrickD = 0.4f;
const float BrickGap = 0.08f;

var brickPalette = new Color[]
{
    new(231,  76,  60),  // red
    new(230, 126,  34),  // orange
    new(241, 196,  15),  // yellow
    new(46,  204, 113),  // green
    new(52,  152, 219),  // blue
};

var window = new Window3D
{
    Title = "Breakout 3D",
    BackgroundColor = new Color(8, 10, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Renderer.DebugDrawEnabled = true;
window.Renderer.AmbientLight = new Color(70, 75, 110);
window.Renderer.DirectionalLight = new DirectionalLight(
    Vector3.Normalize(new Vector3(0.35f, 0.9f, 0.4f)),
    new Color(255, 246, 220));

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0f, PaddleZ + 2.4f),
    Target = new Vector3(0f, 0f, BackZ),
    FieldOfView = MathF.PI / 3f,
    NearPlane = 0.05f,
    FarPlane = 80f,
};
window.Renderer.Camera = camera;

var playField = new PlayField3D();

// ---- Arena: five solid walls (top, bottom, left, right, back).
// Each wall faces inward (normal points toward the arena interior)
// so a one-sided test would already be safe, but we leave them two-
// sided so debug-drift can't trap the ball outside.
float arenaLen = PaddleZ - BackZ;
float arenaMidZ = (PaddleZ + BackZ) * 0.5f;
var arenaHalfExtents = new Vector2(ArenaHalfW, arenaLen * 0.5f);

playField.AddBarriers(
[
    WallBarrier3D.Floor(  new Vector3(0f, -ArenaHalfH, arenaMidZ), arenaHalfExtents),
    WallBarrier3D.Ceiling(new Vector3(0f, +ArenaHalfH, arenaMidZ), arenaHalfExtents),
    WallBarrier3D.Vertical(new Vector3(-ArenaHalfW, 0f, arenaMidZ), Vector3.UnitX,  arenaLen * 0.5f, ArenaHalfH),
    WallBarrier3D.Vertical(new Vector3(+ArenaHalfW, 0f, arenaMidZ), -Vector3.UnitX, arenaLen * 0.5f, ArenaHalfH),
    WallBarrier3D.Vertical(new Vector3(0f, 0f, BackZ), Vector3.UnitZ, ArenaHalfW, ArenaHalfH),
]);

// ---- Bricks: rows colored top-to-bottom; higher rows = more points.
var bricks = new List<BrickBarrier3D>();
float gridW = BrickCols * BrickW + (BrickCols - 1) * BrickGap;
float gridH = BrickRows * BrickH + (BrickRows - 1) * BrickGap;
float gridStartX = -gridW * 0.5f + BrickW * 0.5f;
float gridTopY = +gridH * 0.5f - BrickH * 0.5f;
float brickZ = BackZ + BrickD * 0.5f + 0.6f;

for (int row = 0; row < BrickRows; row++)
{
    var color = brickPalette[row % brickPalette.Length];
    int points = (BrickRows - row) * 10;
    for (int col = 0; col < BrickCols; col++)
    {
        var center = new Vector3(
            gridStartX + col * (BrickW + BrickGap),
            gridTopY   - row * (BrickH + BrickGap),
            brickZ);
        var brick = new BrickBarrier3D(center,
            new Vector3(BrickW * 0.5f, BrickH * 0.5f, BrickD * 0.5f),
            color, points);
        bricks.Add(brick);
        playField.AddBarrier(brick);
    }
}

// ---- Paddle: a thin box that the player slides on the near plane.
var paddle = new Paddle3D(
    halfExtents: new Vector3(PaddleHalfW, PaddleHalfH, PaddleHalfD),
    z: PaddleZ,
    xRange: (-ArenaHalfW + PaddleHalfW, +ArenaHalfW - PaddleHalfW),
    yRange: (-ArenaHalfH + PaddleHalfH, +ArenaHalfH - PaddleHalfH))
{
    EnglishStrength = PaddleEnglish,
};

playField.AddBarrier(paddle);

// ---- Ball.
var ball = new Sprite3D
{
    Visual = MeshVisual3D.Sphere(new Color(245, 250, 255), radius: BallRadius, latitudeSegments: 12, longitudeSegments: 16),
    Position = BallRestPosition(paddle),
    Behaviors = 
    [
        new Motion3D(),
        new BarrierBounce3D
        {
            Restitution = 1f,
            TangentialDamping = 1f,
        },
        // Keeps the ball from devolving into a flat side-to-side
        // grind. After the paddle bounce, if Z is carrying less than
        // MinForwardRatio of the speed, we steal the difference back
        // from X/Y. SpeedClamp runs after this so total speed is still
        // bounded by [Min, Max].1
        new ForwardKickFromPaddle3D
        {
            Paddle = paddle,
            MinForwardRatio = 0.5f,
        },
        new SpeedClamp3D { Min = BallLaunchSpeed * 0.85f, Max = BallMaxSpeed },
    ]
};

playField.AddSprite(ball);

// ---- Controller drives input, game state, paddle position, ball flow.
var controller = new Breakout3DController(
    window.Input, playField, ball, paddle, bricks,
    camera: camera,
    cameraTarget: new Vector3(0f, 0f, BackZ),
    cameraOffsetZ: 2.4f,
    drainZ: DrainZ,
    launchSpeed: BallLaunchSpeed,
    paddleSpeed: PaddleSpeed,
    ballRadius: BallRadius
    );

// ---- HUD: debug-text overlay for score / lives / banner, plus a
// wireframe outline so the player has a visible reticle on the paddle.
var hud = new CustomLayer3D
{
    OnRender = rd =>
    {
        // Paddle as a faint wireframe so it doesn't occlude the arena
        // but the player can see what they're aiming with.
        DebugDraw.DrawBoxCentered(
            paddle.Center,
            new Vector3(PaddleHalfW, PaddleHalfH, PaddleHalfD) * 2f,
            new Color(180, 210, 255));

        // Predicted ball impact on the paddle's near face: drawn on
        // the camera-facing side of the paddle so the paddle's own
        // wireframe doesn't occlude it. Always visible so the player
        // can see where to move the paddle, not just whether they're
        // already lined up.
        float impactZ = paddle.Center.Z - PaddleHalfD;
        if (PredictPaddleHit(ball.Position, ball.Velocity, impactZ,
                ArenaHalfW, ArenaHalfH, BallRadius) is { } hit)
        {
            var markerCenter = new Vector3(hit.X, hit.Y, paddle.Center.Z + PaddleHalfD + 0.05f);
            DebugDraw.DrawSphere(markerCenter, BallRadius, new Color(255, 220, 90));
        }

        // Arena outline (handy for spatial reference).
        DebugDraw.DrawBoxCentered(
            new Vector3(0f, 0f, arenaMidZ),
            new Vector3(ArenaHalfW, ArenaHalfH, arenaLen * 0.5f) * 2f,
            new Color(50, 60, 90));

        DebugDraw.DrawText($"SCORE  {controller.Score,6}", 18f, 16f);
        DebugDraw.DrawText($"LIVES  {controller.Lives}",   18f, 48f);

        if (controller.Banner is { } banner)
        {
            // Approximate center via a fixed offset; the renderer will
            // letterbox the window so this is good-enough placement.
            DebugDraw.DrawText(banner, 18f, 96f, pixelHeight: 44f);
            var hint = controller.HasWon || controller.IsGameOver
                ? "SPACE  new game"
                : "SPACE  launch ball";
            DebugDraw.DrawText(hint, 18f, 152f);
        }
    },
};

// ---- Brick rendering layer: each live brick draws itself.
var brickRenderer = new CustomLayer3D
{
    OnRender = rd =>
    {
        foreach (var b in bricks)
            b.Draw(rd);
    },
};

var scene = new Scene3D
{
    Layers = [ playField, brickRenderer, hud ],
    Behaviors = [ controller ],
};

await scene.RunAsync(window);

Console.WriteLine($"Final Score: {controller.Score}");

// ---- helpers ----------------------------------------------------------

static Vector3 BallRestPosition(Paddle3D p) =>
    new(p.Center.X, p.Center.Y, p.Center.Z - PaddleHalfD - BallRadius - 0.02f);

// Walks the ball forward, reflecting off the four side walls, until it
// reaches <paramref name="targetZ"/> (the paddle's near face). Returns
// the (X, Y) the ball would arrive at, or <see langword="null"/> if it
// isn't currently moving toward the paddle. Ignores brick collisions
// and the back wall, so the prediction stays in sync with the player's
// expectation of "where will the ball end up if nothing changes."
static Vector2? PredictPaddleHit(Vector3 ballPos, Vector3 ballVel,
    float targetZ, float halfW, float halfH, float ballR, int maxBounces = 32)
{
    if (ballVel.Z <= 0f || ballPos.Z >= targetZ)
        return null;

    // Bounding planes for the ball center (inset by the ball radius so
    // the prediction lines up with the actual bounce point).
    float xMin = -halfW + ballR;
    float xMax = +halfW - ballR;
    float yMin = -halfH + ballR;
    float yMax = +halfH - ballR;

    var p = ballPos;
    var v = ballVel;
    for (int i = 0; i < maxBounces; i++)
    {
        float tZ = (targetZ - p.Z) / v.Z;
        float tX = v.X > 0f ? (xMax - p.X) / v.X
                : v.X < 0f ? (xMin - p.X) / v.X
                : float.PositiveInfinity;
        float tY = v.Y > 0f ? (yMax - p.Y) / v.Y
                : v.Y < 0f ? (yMin - p.Y) / v.Y
                : float.PositiveInfinity;
        if (tZ <= tX && tZ <= tY)
        {
            p += v * tZ;
            return new Vector2(p.X, p.Y);
        }
        if (tX <= tY)
        {
            p += v * tX;
            v.X = -v.X;
            p.X = Math.Clamp(p.X, xMin, xMax);
        }
        else
        {
            p += v * tY;
            v.Y = -v.Y;
            p.Y = Math.Clamp(p.Y, yMin, yMax);
        }
    }
    return new Vector2(p.X, p.Y);
}

// ---- types ------------------------------------------------------------

// A thin box the player slides on the near plane. The bounce itself is
// handled by the ball's BarrierBounce3D; this class adds two things:
//   * X/Y clamping so the paddle stays inside the arena's playable
//     rectangle, and
//   * a per-contact "english" injection via SurfaceVelocityAt, so the
//     ball deflects away from where on the paddle face it landed. The
//     existing bounce reads the surface velocity at the contact point
//     and folds it into the reflection.
sealed class Paddle3D : Barrier3D
{
    public Vector3 Center { get; private set; }
    public Vector3 HalfExtents { get; }
    public (float Min, float Max) XRange { get; }
    public (float Min, float Max) YRange { get; }
    public float EnglishStrength { get; set; }

    private Vector3 _previousCenter;
    private Vector3 _velocity;

    public Paddle3D(Vector3 halfExtents, float z,
        (float Min, float Max) xRange,
        (float Min, float Max) yRange)
    {
        HalfExtents = Vector3.Max(halfExtents, Vector3.Zero);
        XRange = xRange;
        YRange = yRange;
        Center = new Vector3(0f, 0f, z);
        _previousCenter = Center;
    }

    public void MoveTo(float x, float y, in UpdateContext context)
    {
        x = Math.Clamp(x, XRange.Min, XRange.Max);
        y = Math.Clamp(y, YRange.Min, YRange.Max);
        var newCenter = new Vector3(x, y, Center.Z);
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        _velocity = dt > 0f ? (newCenter - Center) / dt : Vector3.Zero;
        _previousCenter = Center;
        Center = newCenter;
    }

    public override PosedHitShape3D HitShape =>
        new(new BoxHitShape3D(Vector3.Zero, HalfExtents),
            new Pose3D(Center, Quaternion.Identity, 1f));

    public override Vector3 SurfaceVelocityAt(Vector3 point)
    {
        // Hit position relative to paddle face, normalised over the
        // paddle's half-extents. The bounce code multiplies the surface
        // velocity through and adds the result back into the reflected
        // ball velocity, so a hit on the +X edge punts the ball further
        // +X, etc. — classic Breakout "english".
        var local = point - Center;
        var ex = HalfExtents.X > 0f ? local.X / HalfExtents.X : 0f;
        var ey = HalfExtents.Y > 0f ? local.Y / HalfExtents.Y : 0f;
        var english = new Vector3(ex, ey, 0f) * EnglishStrength;
        return english + _velocity;
    }

    public override void OnHitSprite(Sprite3D hitter, in UpdateContext context)
    {
        Audio.Play(Sounds.Bounce);
    }
}

// One colored brick. Carries its own MeshVisual so it renders in the
// brick layer without the playfield knowing anything about it.
sealed class BrickBarrier3D : Barrier3D
{
    public Vector3 Center { get; }
    public Vector3 HalfExtents { get; }
    public Color Color { get; }
    public int Points { get; }
    public bool IsAlive { get; set; } = true;

    private readonly MeshVisual3D _visual;

    public BrickBarrier3D(Vector3 center, Vector3 halfExtents, Color color, int points)
    {
        Center = center;
        HalfExtents = Vector3.Max(halfExtents, Vector3.Zero);
        Color = color;
        Points = points;
        _visual = MeshVisual3D.Cube(color, size: halfExtents * 2f);
    }

    public override PosedHitShape3D HitShape =>
        IsAlive
            ? new(new BoxHitShape3D(Vector3.Zero, HalfExtents),
                  new Pose3D(Center, Quaternion.Identity, 1f))
            : new(HitShape3D.None, Pose3D.Identity);

    public override void Draw(Renderer3D renderer)
    {
        if (!IsAlive)
            return;
        _visual.Draw(renderer, new Pose3D(Center, Quaternion.Identity, 1f), Color.White, TimeSpan.Zero);
    }

    public override void OnHitSprite(Sprite3D hitter, in UpdateContext context)
    {
        // First contact kills the brick. Score and removal from the
        // playfield are handled by the controller's per-frame sweep.
        IsAlive = false;
        Audio.Play(Sounds.Coin);
    }
}

// After the ball bounces off the paddle, enforces a minimum share of
// total speed on the Z (forward/backward) axis. Without this, the
// english from off-center paddle hits can compound into a near-flat
// X/Y trajectory that ping-pongs between the side walls indefinitely
// without ever returning to the bricks or to the paddle.
sealed class ForwardKickFromPaddle3D : SpriteBehavior3D
{
    public Paddle3D? Paddle { get; set; }

    /// <summary>0..1. Minimum |Vz| / |V| immediately after a paddle hit. 0.5 = Z must hold at least half the speed.</summary>
    public float MinForwardRatio { get; set; } = 0.5f;

    public override void Apply(in UpdateContext context)
    {
        // do nothing.. work happens in OnHitBarrier
    }

    public override void OnHitBarrier(Sprite3D self, Barrier3D barrier, in UpdateContext context)
    {
        if (Paddle is null || !ReferenceEquals(barrier, Paddle))
            return;

        var v = self.Velocity;
        float speed = v.Length();
        if (speed < 1e-4f)
            return;

        float minVz = MinForwardRatio * speed;
        float vz = v.Z;
        if (MathF.Abs(vz) >= minVz)
            return;

        // Rebudget energy: pick a Z component at the minimum magnitude
        // (keep current sign so we don't reverse direction), then scale
        // the X/Y plane to absorb whatever's left, preserving speed.
        float sign = vz < 0f ? -1f : 1f;
        float newVz = sign * minVz;
        float remainingSq = speed * speed - newVz * newVz;
        float remaining = remainingSq > 0f ? MathF.Sqrt(remainingSq) : 0f;

        var xy = new Vector2(v.X, v.Y);
        float xyLen = xy.Length();
        if (xyLen > 1e-4f)
            xy *= remaining / xyLen;
        else
            xy = Vector2.Zero;

        self.Velocity = new Vector3(xy.X, xy.Y, newVz);
    }
}

// Top-level game state: input → paddle, ball launch / drain flow,
// brick clean-up + scoring, win/lose banner.
sealed class Breakout3DController : Behavior
{
    private readonly FrameInput _input;
    private readonly PlayField3D _playField;
    private readonly Sprite3D _ball;
    private readonly Paddle3D _paddle;
    private readonly List<BrickBarrier3D> _bricks;
    private readonly PerspectiveCamera _camera;
    private readonly Vector3 _cameraTarget;
    private readonly float _cameraOffsetZ;
    private readonly float _drainZ;
    private readonly float _launchSpeed;
    private readonly float _paddleSpeed;
    private readonly float _ballRadius;

    private bool _ballInPlay;

    public int Score { get; private set; }
    public int Lives { get; private set; } = 3;
    public bool IsGameOver { get; private set; }
    public bool HasWon { get; private set; }
    public string? Banner { get; private set; } = "SPACE TO START";

    public Breakout3DController(FrameInput input, PlayField3D playField,
        Sprite3D ball, Paddle3D paddle, List<BrickBarrier3D> bricks,
        PerspectiveCamera camera, Vector3 cameraTarget, float cameraOffsetZ,
        float drainZ, float launchSpeed, float paddleSpeed, float ballRadius)
    {
        _input = input;
        _playField = playField;
        _ball = ball;
        _paddle = paddle;
        _bricks = bricks;
        _camera = camera;
        _cameraTarget = cameraTarget;
        _cameraOffsetZ = cameraOffsetZ;
        _drainZ = drainZ;
        _launchSpeed = launchSpeed;
        _paddleSpeed = paddleSpeed;
        _ballRadius = ballRadius;
        SyncCameraToPaddle();
    }

    public override void Apply(in UpdateContext context)
    {
        // --- Paddle input: arrows + WASD.
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        float dx = 0f, dy = 0f;
        if (_input.IsDown(Key.Left)  || _input.IsDown(Key.A)) dx -= 1f;
        if (_input.IsDown(Key.Right) || _input.IsDown(Key.D)) dx += 1f;
        if (_input.IsDown(Key.Up)    || _input.IsDown(Key.W)) dy += 1f;
        if (_input.IsDown(Key.Down)  || _input.IsDown(Key.S)) dy -= 1f;
        var paddleTarget = new Vector2(
            _paddle.Center.X + dx * _paddleSpeed * dt,
            _paddle.Center.Y + dy * _paddleSpeed * dt);
        _paddle.MoveTo(paddleTarget.X, paddleTarget.Y, in context);

        // Camera follows the paddle on X/Y but keeps looking at the
        // fixed point in the middle of the back wall, so the view
        // pans / tilts naturally as the player slides around without
        // the brick grid swimming.
        SyncCameraToPaddle();

        // --- Game state.
        if (IsGameOver || HasWon)
        {
            ParkBallOnPaddle();
            if (_input.WasJustPressed(Key.Space))
                NewGame();
            return;
        }

        if (!_ballInPlay)
        {
            ParkBallOnPaddle();
            if (_input.WasJustPressed(Key.Space))
                LaunchBall();
            return;
        }

        // --- Score bricks that died this frame.
        int aliveCount = 0;
        for (int i = 0; i < _bricks.Count; i++)
        {
            var b = _bricks[i];
            if (b.IsAlive)
            {
                aliveCount++;
            }
            else
            {
                // Award once: removing it from the playfield prevents
                // further hits, and from the list prevents double-count.
                Score += b.Points;
                _playField.RemoveBarrier(b);
                _bricks.RemoveAt(i);
                i--;
            }
        }

        // --- Drain check: ball cleared the paddle's back face.
        if (_ball.Position.Z > _drainZ)
        {
            _ballInPlay = false;
            Lives--;
            if (Lives <= 0)
            {
                IsGameOver = true;
                Banner = "GAME OVER";
                Audio.Play(Melodies.Defeat);
            }
            else
            {
                Banner = $"BALL LOST — {Lives} LEFT";
                Audio.Play(Sounds.Hurt);
            }
            return;
        }

        // --- Win check.
        if (aliveCount == 0)
        {
            HasWon = true;
            _ballInPlay = false;
            Banner = "YOU WIN!";
            Audio.Play(Melodies.Victory);
        }
        else if (Banner is not null)
        {
            Banner = null;
        }
    }

    private void ParkBallOnPaddle()
    {
        _ball.Velocity = Vector3.Zero;
        _ball.Position = new Vector3(_paddle.Center.X, _paddle.Center.Y,
            _paddle.Center.Z - _paddle.HalfExtents.Z - _ballRadius - 0.02f);
    }

    private void SyncCameraToPaddle()
    {
        _camera.Position = new Vector3(
            _paddle.Center.X,
            _paddle.Center.Y,
            _paddle.Center.Z + _cameraOffsetZ);
        _camera.Target = _cameraTarget;
    }

    private void LaunchBall()
    {
        _ballInPlay = true;
        Banner = null;
        // Aim straight down the box with a small random tilt so back-
        // to-back launches don't feel identical.
        var rng = Random.Shared;
        var tilt = new Vector3(
            (float)(rng.NextDouble() - 0.5) * 0.3f,
            (float)(rng.NextDouble() - 0.5) * 0.3f,
            -1f);
        _ball.Velocity = Vector3.Normalize(tilt) * _launchSpeed;
        Audio.Play(Sounds.Laser);
    }

    private void NewGame()
    {
        // V0: pressing Space at GAME OVER / YOU WIN just resets the
        // scoreboard and re-parks the ball. We don't respawn the brick
        // grid here because it'd require retaining the original spawn
        // data; a follow-up could promote the brick factory into a
        // method and call it from here.
        IsGameOver = false;
        HasWon = false;
        Lives = 3;
        Score = 0;
        Banner = _bricks.Count == 0 ? "NO BRICKS — RESTART SAMPLE" : "SPACE TO START";
        _ballInPlay = false;
    }
}
