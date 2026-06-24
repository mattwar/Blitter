#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run BreakoutSample.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// A complete Breakout in one file:
//   * a sliding paddle barrier (mouse + Left/Right keys)
//   * a bouncing ball sprite with proper paddle "english"
//   * colored brick barriers that vanish on hit and award points
//   * floating score popups, lives, ball-launch flow, win/lose banner
//
// Controls:
//   Mouse / Left-Right ........ move paddle
//   Space ..................... launch ball / start new game
//   Esc ....................... quit

using System.Numerics;

using Blitter;
using Blitter.Bits;
using Blitter.Blocks;
using Blitter.Blocks2D;

using SkiaSharp;

const int W = 960;
const int H = 720;

const float WallInset = 12f;
const float PaddleY = H - 70f;
const float PaddleHalfW = 60f;
const float PaddleHalfH = 9f;
const float BallRadius = 8f;
const float BallLaunchSpeed = 460f;
const float BallMaxSpeed = 820f;
const float DrainY = H + 40f;

const int BrickCols = 11;
const int BrickRows = 7;
const float BrickW = 72f;
const float BrickH = 24f;
const float BrickGap = 4f;
const float BrickTop = 90f;

var brickPalette = new Color[]
{
    new(231, 76, 60),    // red
    new(230, 126, 34),   // orange
    new(241, 196, 15),   // yellow
    new(46, 204, 113),   // green
    new(52, 152, 219),   // blue
    new(155, 89, 182),   // purple
    new(236, 112, 199),  // pink
};

var window = new Window2D(W, H)
{
    Title = "Breakout",
    BackgroundColor = new Color(10, 12, 22),    
    FullScreen = true,
    RelativeMouseMode = true,  // hide mouse
    CloseKey = Key.Escape,
    // Render at a fixed 960x720 logical surface; SDL letterboxes it onto
    // whatever the physical fullscreen resolution turns out to be.
    LogicalSize = (W, H),
};

Application.Current.SuppressAccessibilityShortcuts = true;

var hudFont = new Font(["Consolas", "Menlo"], 28, bold: true);
var popupFont = new Font(["Consolas", "Menlo"], 22, bold: true);
var bannerFont = new Font(["Consolas", "Menlo"], 64, bold: true);

var popups = new FloatingTextLayer2D
{
    Font = popupFont,
    DefaultLifetime = TimeSpan.FromSeconds(0.8),
    DefaultVelocity = new Vector2(0f, -90f),
};

var scoreboard = new ScoreLayer2D
{
    Font = hudFont,
    Anchor = HudAnchor.TopLeft,
    Offset = new Vector2(20f, 16f),
    Color = new Color(255, 230, 120),
    Popups = popups,
};

var playField = new PlayField2D { WorldBounds = new Rect(0, 0, W, H) };

// Outer walls: top + two sides. Bottom is open (drain).
playField.AddBarriers(new Barrier2D[]
{
    new LineBarrier2D(new Vector2(WallInset, WallInset),       new Vector2(W - WallInset, WallInset)),       // top
    new LineBarrier2D(new Vector2(WallInset, H),               new Vector2(WallInset, WallInset)),           // left (wound so normal points right)
    new LineBarrier2D(new Vector2(W - WallInset, WallInset),   new Vector2(W - WallInset, H)),               // right
});

// Bricks: rows colored top-to-bottom, more points the higher you reach.
var bricks = new List<Brick>();
float gridWidth = BrickCols * BrickW + (BrickCols - 1) * BrickGap;
float gridX = (W - gridWidth) * 0.5f;
for (int r = 0; r < BrickRows; r++)
{
    var color = brickPalette[r % brickPalette.Length];
    int points = (BrickRows - r) * 10;
    for (int c = 0; c < BrickCols; c++)
    {
        var center = new Vector2(
            gridX + c * (BrickW + BrickGap) + BrickW * 0.5f,
            BrickTop + r * (BrickH + BrickGap) + BrickH * 0.5f);
        var brick = new Brick(center, BrickW, BrickH, color, points)
        {
            Scoreboard = scoreboard,
            Popups = popups,
        };
        bricks.Add(brick);
        playField.AddBarrier(brick);
    }
}

// Paddle is a fat horizontal capsule barrier. We slide it on X each
// frame from input and reflect the ball ourselves on contact so we
// can apply classic Breakout "english" (deflection depends on where
// on the paddle the ball lands).
var paddle = new Paddle(PaddleHalfW, PaddleHalfH)
{
    Center = new Vector2(W * 0.5f, PaddleY),
    XMin = WallInset + PaddleHalfW,
    XMax = W - WallInset - PaddleHalfW,
};
playField.AddBarrier(paddle);

// Ball with a real hit radius. Visual is procedural so we can keep
// the file self-contained.
var ball = new BreakoutBall(BallRadius)
{
    Center = BallRestPosition(paddle),
    Behaviors =
    [
        new Motion2D(),
        new SurfaceBounce2D
        {
            Restitution = 1f,
            TangentialDamping = 1f,
            Bounced = new PlayBounceSound(),
        },
        new SpeedClamp2D { Min = 240f, Max = BallMaxSpeed },
    ],
};
playField.AddSprite(ball);

// HUD layer: lives, banner text.
var controller = new BreakoutController(window.Input, window.Renderer, ball, paddle, bricks,
    drainY: DrainY,
    launchSpeed: BallLaunchSpeed);

var hud = new BreakoutHud(controller, hudFont, bannerFont, W, H);

var scene = new Scene2D
{
    Layers  = [ playField, popups, scoreboard, hud ],
    Behaviors = [ controller ],
};

await scene.RunAsync(window);

Console.WriteLine($"Final Score: {scoreboard.Score}");

// ---- helpers ----------------------------------------------------------

static Vector2 BallRestPosition(Paddle paddle) =>
    new(paddle.Center.X, paddle.Center.Y - paddle.HalfHeight - BallRadius - 2f);


// ---- sprites, barriers, behaviors -------------------------------------

// Plays the bounce SFX whenever the ball bounces off a surface.
sealed class PlayBounceSound : IEventHandler<SurfaceBounced2DEventArgs>
{
    public void OnEvent(in SurfaceBounced2DEventArgs e) => Audio.Play(Sounds.Bounce, 0.35f);
}

// HUD overlay: lives counter and the win/lose banner with key hint.
sealed class BreakoutHud(BreakoutController controller, Font hudFont, Font bannerFont, int w, int h) : Layer2D
{
    protected override void DrawContent(Renderer2D rd)
    {
        using var _ = rd.PushState();
        rd.Camera = null;

        var livesText = $"LIVES {controller.Lives}";
        var livesSize = hudFont.Measure(livesText);
        hudFont.DrawText(rd, livesText, new Color(220, 230, 255),
            w - livesSize.X - 20f, 16f);

        if (controller.Banner is { } banner)
        {
            var size = bannerFont.Measure(banner);
            bannerFont.DrawText(rd, banner, new Color(255, 240, 200),
                (w - size.X) * 0.5f, h * 0.42f);

            var hint = controller.HasWon || controller.IsGameOver
                ? "SPACE  new game"
                : "SPACE  launch ball";
            var hintSize = hudFont.Measure(hint);
            hudFont.DrawText(rd, hint, new Color(180, 200, 230),
                (w - hintSize.X) * 0.5f, h * 0.42f + size.Y + 12f);
        }
    }
}

// The ball: a small shaded disc. Its image supplies both the look and
// the (circular) collision shape, so there's no Draw override and no
// explicit CollisionShape2D — the collider derives the hit circle from
// the visual, scaled by the sprite's Transform.
sealed class BreakoutBall : Sprite2D
{
    public float Radius { get; }

    public BreakoutBall(float radius)
    {
        Radius = radius;

        var image = MakeBall(32);
        Image = new ImageSource { Texture = image, Hit = HitShapeHint.Circle };
        Scale = (radius * 2f) / image.Width;
    }

    // A white, anti-aliased ball with a soft top-left highlight.
    private static Bitmap MakeBall(int size)
    {
        var image = Bitmap.Create(size, size);
        image.DrawCanvas(canvas =>
        {
            canvas.Clear(SKColors.Transparent);

            var c = size / 2f;
            var r = size / 2f - 1f;
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(
                    center: new SKPoint(c - r * 0.3f, c - r * 0.3f),
                    radius: r * 1.4f,
                    colors:
                    [
                        new SKColor(255, 255, 255),
                        new SKColor(228, 234, 246),
                        new SKColor(170, 182, 205),
                    ],
                    colorPos: [0f, 0.55f, 1f],
                    mode: SKShaderTileMode.Clamp),
            };
            canvas.DrawCircle(c, c, r, paint);
        });
        return image;
    }
}

// Optional ball-side behavior that clamps Speed each tick.
// Keeps the ball lively even when bricks/paddle introduce damping.
sealed class SpeedClamp2D : Behavior, IUpdatable
{
    public float Min { get; set; }
    public float Max { get; set; }

    private Sprite2D _target = null!;

    protected override void OnAttach(IEntity entity) => _target = (Sprite2D)entity;

    public void Update(in UpdateContext context)
    {
        var target = _target;
        if (target.Speed > 0f && target.Speed < Min)
            target.Speed = Min;
        else if (target.Speed > Max)
            target.Speed = Max;
    }
}

// A solid horizontal capsule barrier the player slides left/right.
// Reflects the ball with a hit-position-dependent deflection (the
// classic "the spot on the paddle controls the bounce" trick).
sealed class Paddle : Barrier2D
{
    public Vector2 Center { get => Transform.Position; set => Transform.Position = value; }
    public float HalfWidth { get; }
    public float HalfHeight { get; }

    /// <summary>X clamp range applied when <see cref="Center"/> is moved.</summary>
    public float XMin { get; set; } = float.NegativeInfinity;
    public float XMax { get; set; } = float.PositiveInfinity;

    /// <summary>X velocity from the previous frame; fed into the ball as surface velocity.</summary>
    public float VelocityX { get; private set; }

    private Vector2 _previousCenter;
    private TimeSpan _lastDt;

    public Paddle(float halfWidth, float halfHeight)
    {
        HalfWidth = halfWidth;
        HalfHeight = halfHeight;
        this.GetOrAddTrait<CollisionShape2D>().Shape = new CapsuleHitShape2D(
            new Vector2(-HalfWidth, 0f),
            new Vector2(HalfWidth, 0f),
            HalfHeight);
        AddBehavior(new HitResponse());
    }

    private sealed class HitResponse : Behavior, IHittable2D
    {
        private Paddle _host = null!;
        protected override void OnAttach(IEntity entity) => _host = (Paddle)entity;
        void IHittable2D.OnHit(in Hit2D hit) => _host.OnHit(_host, hit.Other);
    }

    public void MoveTo(float x, in UpdateContext context)
    {
        var clamped = Math.Clamp(x, XMin, XMax);
        _previousCenter = Center;
        Center = new Vector2(clamped, Center.Y);
        _lastDt = context.ElapsedSinceLastUpdate;
        var dt = (float)_lastDt.TotalSeconds;
        VelocityX = dt > 0f ? (Center.X - _previousCenter.X) / dt : 0f;
    }

    private void OnHit(IEntity self, IEntity other)
    {
        if (other is not BreakoutBall ball)
            return;

        // Only act when the ball is heading downward. Avoids
        // sticky/double-bounces when the ball is grazing the side.
        var v = Sprite2D.GetVelocity(ball.Speed, ball.Heading);
        if (v.Y <= 0f && ball.Center.Y < Center.Y)
            return;

        // Compute hit offset: -1 at left edge, 0 dead center, +1 at right.
        var offset = (ball.Center.X - Center.X) / HalfWidth;
        offset = Math.Clamp(offset, -1f, 1f);

        // Map to a deflection angle in [-MaxDeflection, +MaxDeflection]
        // measured from straight up.
        const float MaxDeflectionDeg = 65f;
        float deflectDeg = offset * MaxDeflectionDeg;
        float rad = deflectDeg * MathF.PI / 180f;

        // Preserve speed (with a tiny boost so play stays brisk) and
        // ensure the ball is moving up.
        const float minBounceSpeed = 460f;
        var speed = MathF.Max(ball.Speed, minBounceSpeed) * 1.02f;
        var newV = new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * speed;

        // Add a fraction of the paddle's lateral motion so flicking
        // the paddle imparts spin.
        newV.X += VelocityX * 0.30f;

        // Push the ball just above the paddle so it doesn't get
        // re-detected next substep.
        var pushY = Center.Y - HalfHeight - ball.Radius - 0.5f;
        if (ball.Center.Y > pushY)
            ball.Center = new Vector2(ball.Center.X, pushY);

        (ball.Speed, ball.Heading) = Sprite2D.GetSpeedAndHeading(newV);
        Audio.Play(Sounds.Blip, 0.5f);
    }

    public override void Draw(Renderer2D renderer)
    {
        var a = new Vector2(Center.X - HalfWidth, Center.Y);
        var b = new Vector2(Center.X + HalfWidth, Center.Y);
        renderer.DrawThickLine(a, b, new Color(220, 220, 235), HalfHeight * 2f);
        // Bright center pip — a visual cue for the "dead-center" sweet spot.
        renderer.DrawDisc(Center, HalfHeight * 0.55f, new Color(255, 240, 180));
    }
}

// A breakable rectangular brick. On the first hit it scores, plays a
// pop, spawns a "+points" popup, and removes itself from the playfield.
sealed class Brick : Barrier2D
{
    public Vector2 Center => Transform.Position;
    public float HalfWidth { get; }
    public float HalfHeight { get; }
    public Color Color { get; }
    public long Points { get; }
    public ScoreLayer2D? Scoreboard { get; set; }
    public FloatingTextLayer2D? Popups { get; set; }

    public bool IsAlive { get; private set; } = true;

    public void Revive(PlayField2D playField)
    {
        if (IsAlive) return;
        IsAlive = true;
        this.GetOrAddTrait<CollisionShape2D>().Shape =
            new BoxHitShape2D(Vector2.Zero, new Vector2(HalfWidth, HalfHeight));
        playField.AddBarrier(this);
    }

    public Brick(Vector2 center, float width, float height, Color color, long points)
    {
        Transform.Position = center;
        HalfWidth = width * 0.5f;
        HalfHeight = height * 0.5f;
        Color = color;
        Points = points;
        this.GetOrAddTrait<CollisionShape2D>().Shape =
            new BoxHitShape2D(Vector2.Zero, new Vector2(HalfWidth, HalfHeight));
        AddBehavior(new HitResponse());
    }

    private sealed class HitResponse : Behavior, IHittable2D
    {
        private Brick _host = null!;
        protected override void OnAttach(IEntity entity) => _host = (Brick)entity;
        void IHittable2D.OnHit(in Hit2D hit) => _host.OnHit(_host, hit.Other);
    }

    private void OnHit(IEntity self, IEntity other)
    {
        if (!IsAlive) return;
        if (other is not BreakoutBall ball) return;

        // Determine which face the ball came in through, using its
        // current center relative to the brick. Reflect just that axis.
        // Compare normalized overlaps on each axis so a ball hitting
        // a corner picks the axis with the deeper penetration.
        var dx = ball.Center.X - Center.X;
        var dy = ball.Center.Y - Center.Y;
        var overlapX = (HalfWidth + ball.Radius) - MathF.Abs(dx);
        var overlapY = (HalfHeight + ball.Radius) - MathF.Abs(dy);

        var v = Sprite2D.GetVelocity(ball.Speed, ball.Heading);
        if (overlapX < overlapY)
        {
            // Side hit -> flip X.
            v.X = MathF.Abs(v.X) * MathF.Sign(dx);
            // Push out along X to avoid re-detection.
            ball.Center = new Vector2(
                Center.X + MathF.Sign(dx) * (HalfWidth + ball.Radius + 0.5f),
                ball.Center.Y);
        }
        else
        {
            // Top/bottom hit -> flip Y.
            v.Y = MathF.Abs(v.Y) * MathF.Sign(dy);
            ball.Center = new Vector2(
                ball.Center.X,
                Center.Y + MathF.Sign(dy) * (HalfHeight + ball.Radius + 0.5f));
        }
        (ball.Speed, ball.Heading) = Sprite2D.GetSpeedAndHeading(v);

        // Score + popup + sound.
        Scoreboard?.Add(Points, Center);
        if (Popups is null && Scoreboard is null)
        {
            // (no-op — scoreboard handles its own popup forwarding)
        }
        Audio.Play(Sounds.Coin, 0.35f);

        IsAlive = false;
        // Drop our collision geometry so the same-frame sprite-direction
        // dispatch (the ball's SurfaceBounce2D) sees no contact and can't
        // bounce off a brick we just cleared.
        this.GetOrAddTrait<CollisionShape2D>().Shape = HitShape2D.None;
        // Remove on next safe boundary so the collision pass doesn't
        // see this barrier again this frame.
        ball.PlayField.RemoveBarrier(this);
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!IsAlive) return;
        renderer.DrawColor = Color;
        renderer.DrawFillRect(new Rect(
            Center.X - HalfWidth, Center.Y - HalfHeight,
            HalfWidth * 2f, HalfHeight * 2f));

        // Faux top/left highlight.
        var hi = new Color(
            (byte)Math.Min(255, Color.R + 60),
            (byte)Math.Min(255, Color.G + 60),
            (byte)Math.Min(255, Color.B + 60));
        renderer.DrawColor = hi;
        renderer.DrawFillRect(new Rect(
            Center.X - HalfWidth, Center.Y - HalfHeight,
            HalfWidth * 2f, 2f));
        renderer.DrawFillRect(new Rect(
            Center.X - HalfWidth, Center.Y - HalfHeight,
            2f, HalfHeight * 2f));

        // Faux bottom/right shadow.
        var lo = new Color(
            (byte)(Color.R * 0.55f),
            (byte)(Color.G * 0.55f),
            (byte)(Color.B * 0.55f));
        renderer.DrawColor = lo;
        renderer.DrawFillRect(new Rect(
            Center.X - HalfWidth, Center.Y + HalfHeight - 2f,
            HalfWidth * 2f, 2f));
        renderer.DrawFillRect(new Rect(
            Center.X + HalfWidth - 2f, Center.Y - HalfHeight,
            2f, HalfHeight * 2f));
    }
}

// Scene-wide game loop: input, paddle motion, launch flow, lives,
// drain detection, win/lose state, level reset.
sealed class BreakoutController : Behavior, IUpdatable
{
    private readonly FrameInput _input;
    private readonly Renderer2D _renderer;
    private readonly BreakoutBall _ball;
    private readonly Paddle _paddle;
    private readonly List<Brick> _bricks;
    private readonly float _drainY;
    private readonly float _launchSpeed;

    private bool _ballInPlay;
    private readonly Random _rng = new();

    private float _paddleTargetX;
    private bool _paddleTargetInitialized;

    public int Lives { get; private set; } = 3;
    public bool IsGameOver { get; private set; }
    public bool HasWon { get; private set; }
    public string? Banner { get; private set; } = "BREAKOUT";

    public BreakoutController(
        FrameInput input,
        Renderer2D renderer,
        BreakoutBall ball,
        Paddle paddle,
        List<Brick> bricks,
        float drainY,
        float launchSpeed)
    {
        _input = input;
        _renderer = renderer;
        _ball = ball;
        _paddle = paddle;
        _bricks = bricks;
        _drainY = drainY;
        _launchSpeed = launchSpeed;
    }

    public void Update(in UpdateContext context)
    {
        var scene = (Scene2D)this.Entity;

        // --- Paddle: mouse delta drives the target; arrows nudge it.
        // RelativeMouseMode hides the cursor and pins it, so absolute
        // MousePosition is useless here. We accumulate MouseDelta and
        // scale it from window pixels into the renderer's logical
        // 960x720 space so sensitivity matches the design resolution
        // regardless of the actual fullscreen size.
        if (!_paddleTargetInitialized)
        {
            _paddleTargetX = _paddle.Center.X;
            _paddleTargetInitialized = true;
        }

        var presentRect = _renderer.LogicalRepresentationRect;
        var (logicalW, _) = _renderer.LogicalSize;
        float pxToLogical = presentRect.Width > 0f && logicalW > 0
            ? logicalW / presentRect.Width
            : 1f;

        _paddleTargetX += _input.MouseDelta.X * pxToLogical;

        const float keySpeed = 720f;
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (_input.IsDown(Key.Left))
            _paddleTargetX -= keySpeed * dt;
        if (_input.IsDown(Key.Right))
            _paddleTargetX += keySpeed * dt;

        // Clamp accumulator so pushing into a wall doesn't build up
        // a phantom offset that has to be unwound.
        _paddleTargetX = Math.Clamp(_paddleTargetX, _paddle.XMin, _paddle.XMax);

        _paddle.MoveTo(_paddleTargetX, in context);

        // --- Game state transitions.
        if (IsGameOver || HasWon)
        {
            // Idle ball on paddle.
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

        // --- In play: drain check.
        if (_ball.Center.Y > _drainY)
        {
            // Lose a life.
            _ballInPlay = false;
            Audio.Play(Sounds.Hurt, 0.6f);
            Lives--;
            if (Lives <= 0)
            {
                IsGameOver = true;
                Banner = "GAME OVER";
                Audio.Play(Sounds.RoarDown, 0.5f);
            }
            else
            {
                Banner = $"BALL LOST — {Lives} LEFT";
            }
            return;
        }

        // --- Win check.
        bool anyAlive = false;
        foreach (var b in _bricks)
            if (b.IsAlive) { anyAlive = true; break; }
        if (!anyAlive)
        {
            HasWon = true;
            Banner = "YOU WIN!";
            _ballInPlay = false;
            Audio.Play(Sounds.PowerUp, 0.6f);
        }
        else if (Banner is not null && _ballInPlay)
        {
            Banner = null;
        }
    }

    private void ParkBallOnPaddle()
    {
        _ball.Speed = 0f;
        _ball.Center = new Vector2(
            _paddle.Center.X,
            _paddle.Center.Y - _paddle.HalfHeight - _ball.Radius - 2f);
    }

    private void LaunchBall()
    {
        _ballInPlay = true;
        Banner = null;
        // Random angle in [-35°, +35°] from straight up.
        float deg = ((float)_rng.NextDouble() * 2f - 1f) * 35f;
        float rad = deg * MathF.PI / 180f;
        var v = new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * _launchSpeed;
        (_ball.Speed, _ball.Heading) = Sprite2D.GetSpeedAndHeading(v);
        Audio.Play(Sounds.Select, 0.5f);
    }

    private void NewGame()
    {
        var playField = _ball.PlayField;
        foreach (var b in _bricks)
            b.Revive(playField);

        Lives = 3;
        IsGameOver = false;
        HasWon = false;
        Banner = "BREAKOUT";
        _ballInPlay = false;
    }
}
