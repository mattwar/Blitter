#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run Pinball.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// pinball: its pinball.. 
// Keep the ball bouncing off the bumpers to earn points.
// Use the flippers to keep it in play.

using System.Numerics;

using Blitter;
using Blitter.Bits;
using Blitter.Blocks;
using Blitter.Blocks2D;

using SkiaSharp;

// logical size
const int W = 720;
const int H = 960;

const float BallRadius = 18f;
const float DrainY = 920f;

// Ball drops from the top-center of the cabinet. Random horizontal
// component on launch sends it tumbling through the bumpers instead
// of straight down a column.
var plungerSpawn = new Vector2(W / 2f, 70f);

var window = new Window2D(W, H)
{
    Title = "Pinball Blaster",
    BackgroundColor = new Color(8, 12, 24),
    FullScreen = true,
    RelativeMouseMode = true, // hides the mouse
    CloseKey = Key.Escape,
    LogicalSize = (W, H),
};

// Stop Shift×5 / right-Shift-hold from triggering the Windows
// Sticky/Filter Keys prompt while we're playing.
Application.Current.SuppressAccessibilityShortcuts = true;

var ballImage = MakeChromeBall(64);

var scoreFont = new Font(["Consolas", "Menlo"], 36, bold: true);
var bannerFont = new Font(["Consolas", "Menlo"], 96, bold: true);
var popupFont = new Font(["Consolas", "Menlo"], 28, bold: true);

var popups = new FloatingTextLayer2D
{
    Font = popupFont,
    DefaultLifetime = TimeSpan.FromSeconds(0.9),
    DefaultVelocity = new Vector2(0f, -120f),
};

var scoreboard = new ScoreLayer2D
{
    Font = scoreFont,
    Anchor = HudAnchor.TopLeft,
    Offset = new Vector2(20f, 20f),
    Color = new Color(255, 215, 0),
    Popups = popups,
    PositivePopupColor = new Color(255, 215, 0),
};

var camera = new Camera2D { Position = new Vector2(W / 2f, H / 2f) };
window.Renderer.Camera = camera;

var shaker = new CameraShake2D { Camera = camera, MaxOffset = 14f, Decay = 1.8f };

var playField = new PlayField2D
{
    WorldBounds = new Rect(0, 0, W, H),
};

// Outer walls (drain gap left in the middle of the bottom V).
const float WallInset = 16f;
const float CenterGapOffset = 111f;

// side walls
foreach (var barrier in new Barrier2D[]
{
    new Wall(new Vector2(WallInset, WallInset), new Vector2(WallInset, 820f)), // left
    new Wall(new Vector2(W - WallInset, WallInset), new Vector2(W - WallInset, 820f)), // right
    new Wall(new Vector2(WallInset, WallInset), new Vector2(W - WallInset, WallInset)), // top
    new Wall(new Vector2(WallInset, 820f), new Vector2(W / 2f - CenterGapOffset, 860f)), // bottom left
    new Wall(new Vector2(W - WallInset, 820f), new Vector2(W / 2f + CenterGapOffset, 860f)), // bottom right
})
    playField.AddEntity(barrier);

// Resolve loose asset files next to this source file.
Application.Current.SetCallerAssetFolder();

var bumperSound = Sound.Load("bumper.wav");
var flipperSound = Sound.Load("flipper.wav");
var slingshotSound = Sound.Load("slingshot.wav");

// circular bumpers
foreach (var barrier in new Barrier2D[]
{
    new Bumper(180f, 280f, 44f) { Tint=new Color(255, 90, 120), HitSound=bumperSound, Scoreboard=scoreboard, Shaker=shaker, ShakeTrauma=0.40f },
    new Bumper(420f, 230f, 44f) { Tint=new Color(90, 200, 255), HitSound=bumperSound, Scoreboard=scoreboard, Shaker=shaker, ShakeTrauma=0.40f },
    new Bumper(300f, 410f, 50f) { Tint=new Color(140, 255, 140), HitSound=bumperSound, Scoreboard=scoreboard, Shaker=shaker, ShakeTrauma=0.50f },
    new Bumper(140f, 580f, 22f) { Tint=new Color(255, 200, 90), HitSound=bumperSound, Scoreboard=scoreboard, Shaker=shaker, ShakeTrauma=0.22f },
    new Bumper(580f, 580f, 22f) { Tint=new Color(255, 200, 90), HitSound=bumperSound, Scoreboard=scoreboard, Shaker=shaker, ShakeTrauma=0.22f },
})
    playField.AddEntity(barrier);

// slingshots: line barriers that bounce on one-side only
foreach (var barrier in new Barrier2D[]
{
    // left
    new Slingshot(
        new Vector2(WallInset + 8f, 620f), 
        new Vector2(220f, 740f)) 
    { 
        HitSound = slingshotSound 
    },
    // right
    new Slingshot(
        new Vector2(500f, 740f), 
        new Vector2(W - WallInset - 8f, 620f)) 
    { 
        HitSound = slingshotSound 
    }
})
    playField.AddEntity(barrier);

// Flippers
var flipperLeft = new Flipper
{
    HitSound = flipperSound,
    Pivot = new Vector2(W / 2f - CenterGapOffset, 870f),
    Length = 90f,
    Radius = 10f,
    RestAngleDeg = 28f, // down-right
    ActiveAngleDeg = -22f, // up-right
    SnapDegPerSec = 900f,
};

var flipperRight = new Flipper
{
    HitSound = flipperSound,
    Pivot = new Vector2(W / 2f + CenterGapOffset, 870f),
    Length = 90f,
    Radius = 10f,
    RestAngleDeg = 180f - 28f, // down-left
    ActiveAngleDeg = 180f + 22f, // up-left
    SnapDegPerSec = 1900f,
};

playField.AddEntity(flipperLeft);
playField.AddEntity(flipperRight);

// The "ball"
var ball = new Pinball
{
    Image = ballImage,
    Center = plungerSpawn,
    Scale = (BallRadius * 2f) / ballImage.Width,
    Behaviors = 
    [
        new Gravity2D { Acceleration = new Vector2(0f, 1400f), MaxFallSpeed = 1600f },
        new Motion2D(),
        new SurfaceBounce2D
        {
            Restitution = 0.82f,
            TangentialDamping = 0.985f,
        },
        shaker
    ]
};

playField.AddEntity(ball);

// Scene-wide pinball game controls.
var gameController = new PinballGameController(window.Input, ball, flipperLeft, flipperRight, plungerSpawn, DrainY);

// The drain isn't an actual barrier
// it is drawn as its own 'background' layer
var drainBand = new DrainBandLayer(WallInset, DrainY, W, H);

// The HUD with score and other text
var hud = new PinballHud(gameController, scoreFont, H);

// The scene puts it all togther.
var scene = new Scene2D
{
    Entities = 
    [ 
        drainBand, 
        playField, 
        popups, 
        scoreboard, 
        hud 
    ],
    Behaviors =
    [
        gameController,
    ],
};

// Run the scene (makes the game run within the window)
await scene.RunAsync(window);

Console.WriteLine($"Final Score: {scoreboard.Score}");


//--------------------------------------------------------------------------------------------------------------------------
// sprites, barriers, behaviors and helpers

static Bitmap MakeChromeBall(int size)
{
    var image = Bitmap.Create(size, size);

    image.DrawCanvas(canvas =>
    {
        canvas.Clear(SKColors.Transparent);

        var cx = size / 2f;
        var cy = size / 2f;
        var r  = size / 2f - 1f;

        // Sphere body: off-center radial gradient simulates a light
        // from the upper-left. Stops mimic chrome — bright highlight,
        // mid silver, deep shadow rim.
        using (var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                center: new SKPoint(cx - r * 0.35f, cy - r * 0.40f),
                radius: r * 1.45f,
                colors:
                [
                    new SKColor(255, 255, 255),
                    new SKColor(225, 230, 240),
                    new SKColor(150, 160, 180),
                    new SKColor(35, 40, 55),
                    new SKColor(10, 12, 18),
                ],
                colorPos: [0f, 0.12f, 0.45f, 0.85f, 1f],
                mode: SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawCircle(cx, cy, r, paint);
        }

        // A small, mostly opaque white blob up-and-left of center
        // to look like a highlight reflection.
        using (var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                center: new SKPoint(cx - r * 0.40f, cy - r * 0.45f),
                radius: r * 0.30f,
                colors:
                [
                    new SKColor(255, 255, 255, 235),
                    new SKColor(255, 255, 255, 0),
                ],
                colorPos: [0f, 1f],
                mode: SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawCircle(cx - r * 0.40f, cy - r * 0.45f, r * 0.30f, paint);
        }

        // Faint reflected-light arc on the lower-right rim to make the sphere look 
        // like a real metal ball by hinting at ambient bounce.
        using (var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                center: new SKPoint(cx + r * 0.55f, cy + r * 0.55f),
                radius: r * 0.85f,
                colors:
                [
                    new SKColor(180, 200, 235, 90),
                    new SKColor(180, 200, 235, 0),
                ],
                colorPos: [0f, 1f],
                mode: SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawCircle(cx, cy, r, paint);
        }
    });

    return image;
}

// Background layer for the drain band beneath the playfield.
sealed class DrainBandLayer(float wallInset, float drainY, int w, int h) : Entity, IDrawable2D
{
    public void Draw(Renderer2D rd)
    {
        rd.DrawColor = new Color(70, 20, 30);
        rd.DrawFillRect(new Rect(wallInset, drainY, w - 2f * wallInset, h - drainY));
    }
}

// HUD overlay: ball status text and the key hint line.
sealed class PinballHud(PinballGameController gameController, Font scoreFont, int h) : Entity, IDrawable2D
{
    public void Draw(Renderer2D rd)
    {
        using var _ = rd.PushState();
        rd.Camera = null;

        var status = gameController.BallInPlay
            ? $"BALL {gameController.BallNumber}"
            : "SPACE TO DROP";
        scoreFont.DrawText(rd, status, Color.White, 20f, 64f);

        // Subtle hint line for the keys.
        rd.DrawColor = new Color(180, 200, 230);
        rd.DrawDebugText(20, h - 28, "Shift flippers   <- -> nudge   Space drop   Esc quit", scale: 1.5f);
    }
}

// the pinball
sealed class Pinball : Sprite2D
{
}

// Generic outer-wall / floor / ceiling segment.
sealed class Wall : LineBarrier2D
{
    public Wall(Vector2 start, Vector2 end)
        : base(start, end)
    {
        PhysicsMaterial = PhysicsMaterial.Metal;
    }

    public override void Draw(Renderer2D renderer)
    {
        renderer.DrawThickLine(Start, End, new Color(120, 170, 230), 5f);
    }
}

// Bumper
sealed class Bumper : CircleBarrier2D
{
    public ScoreLayer2D? Scoreboard { get; set; }
    public CameraShake2D? Shaker { get; set; }
    public float ShakeTrauma { get; set; } = 0.55f;
    public Sound? HitSound { get; set;}
    public Color Tint { get; set; } = Color.White;

    public Bumper(float x, float y, float radius)
        : base(x, y, radius)
    {
        PhysicsMaterial = new PhysicsMaterial(Restitution: 0.95f, Friction: 0.05f, KickSpeed: 320f);
        GetOrAddBehavior<HitResponse>();
    }

    public override void Draw(Renderer2D renderer)
    {
        renderer.DrawDisc(Center, Radius, Tint);
    }

    private sealed class HitResponse : Behavior, IHittable2D
    {
        private Bumper _host = null!;
        protected override void OnAttach(IEntity entity) => _host = (Bumper)entity;
        void IHittable2D.OnHit(in Hit2D hit) => _host.OnHit(_host, hit.Other);
    }

    private void OnHit(IEntity self, IEntity other)
    {
        if (other is not Sprite2D hitter) return;

        if (this.Scoreboard != null)
        {
            this.Scoreboard.PositivePopupColor = this.Tint;
            this.Scoreboard.Add(100, hitter.Center);
        }

        this.Shaker?.AddTrauma(this.ShakeTrauma);

        if (HitSound != null)
            Audio.Play(HitSound, 0.6f);
    }
}

// slingshot — a one-sided line bumper
sealed class Slingshot : LineBarrier2D
{
    public Sound? HitSound { get; set; }

    public ScoreLayer2D? Scoreboard { get; set; }
    public long ScorePerHit { get; set; } = 25;

    public CameraShake2D? Shaker { get; set; }
    public float ShakeTrauma { get; set; } = 0.35f;

    public Slingshot(Vector2 start, Vector2 end)
        : base(start, end)
    {
        OneSided = true;
        PhysicsMaterial = new PhysicsMaterial(Restitution: 0.95f, Friction: 0.05f, KickSpeed: 180f);
        GetOrAddBehavior<HitResponse>();
    }

    public override void Draw(Renderer2D renderer)
    {
        renderer.DrawThickLine(Start, End, new Color(255, 150, 80), 5f);
    }

    private sealed class HitResponse : Behavior, IHittable2D
    {
        private Slingshot _host = null!;
        protected override void OnAttach(IEntity entity) => _host = (Slingshot)entity;
        void IHittable2D.OnHit(in Hit2D hit) => _host.OnHit(_host, hit.Other);
    }

    private void OnHit(IEntity self, IEntity other)
    {
        if (other is not Sprite2D hitter) return;

        if (this.HitSound is {} hs)
        {
            Audio.Play(hs, 0.7f);
        }

        if (this.Scoreboard is {} sb)
        {
            sb.PositivePopupColor = new Color(200, 200, 255);
            sb.Add(this.ScorePerHit, hitter.Center);
        }
        
        if (this.Shaker is {} shaker)
        {
            shaker.AddTrauma(this.ShakeTrauma);
        }
    }
}

// Flipper: a swinging barrier with visual and hit sounds
sealed class Flipper : SwingArmBarrier2D
{
    public Sound? HitSound { get; set;}

    protected override void OnPressed(in EntityUpdateContext context)
    {
        if (this.HitSound is {} sound)
            Audio.Play(sound, 0.5f);
    }

    public override void Draw(Renderer2D renderer)
    {
        var tint = Pressed
            ? new Color(255, 230, 120)
            : new Color(220, 220, 235);
        renderer.DrawThickLine(Pivot, Tip, tint, Radius * 2f);
        renderer.DrawDisc(Pivot, Radius + 2f, new Color(180, 180, 200));
    }
}

// Coordinates pinball gameplay controls and ball lifecycle at scene scope.
sealed class PinballGameController : Behavior, IUpdatable
{
    private readonly FrameInput _input;
    private readonly Pinball _ball;
    private readonly Flipper _flipperLeft;
    private readonly Flipper _flipperRight;
    private readonly Vector2 _spawn;
    private readonly float _drainY;

    public PinballGameController(
        FrameInput input,
        Pinball ball,
        Flipper flipperLeft,
        Flipper flipperRight,
        Vector2 spawn,
        float drainY)
    {
        _input = input;
        _ball = ball;
        _flipperLeft = flipperLeft;
        _flipperRight = flipperRight;
        _spawn = spawn;
        _drainY = drainY;
        BallInPlay = false;
        BallNumber = 0;
    }

    /// <summary>
    /// True between launch and drain.
    /// </summary>
    public bool BallInPlay { get; private set; }

    /// <summary>
    /// Increments on each launch so the HUD can show the current ball number.
    /// </summary>
    public int BallNumber { get; private set; }

    private readonly Random _rng = new();

    public void Update(in EntityUpdateContext context)
    {
        // Flippers: each frame, drive Pressed off the shift keys.
        // The barriers handle slewing and surface velocity.
        _flipperLeft.Pressed = _input.IsDown(Key.LShift);
        _flipperRight.Pressed = _input.IsDown(Key.RShift);

        if (!BallInPlay)
        {
            // Park the ball at the drop point and freeze it. Space
            // drops it with a small random horizontal kick so each
            // ball plays differently.
            _ball.Center = _spawn;
            _ball.Speed = 0f;
            if (_input.WasJustPressed(Key.Space))
            {
                BallInPlay = true;
                BallNumber++;
                // Heading: 180 = straight down. ±25° gives some lateral
                // entry so the ball doesn't fall through the same gap
                // every time.
                _ball.Heading = 180f + ((float)_rng.NextDouble() - 0.5f) * 50f;
                _ball.Speed = 380f;
                Audio.Play(Sounds.Jump, 0.7f);
            }
            return;
        }

        // Nudge: tap arrows to give the ball a small lateral impulse.
        // Real pinball tilt warning is out of scope.
        const float nudge = 90f;
        if (_input.WasJustPressed(Key.Left))
        {
            var v = Sprite2D.GetVelocity(_ball.Speed, _ball.Heading) + new Vector2(-nudge, 0f);
            (_ball.Speed, _ball.Heading) = Sprite2D.GetSpeedAndHeading(v);
        }
        if (_input.WasJustPressed(Key.Right))
        {
            var v = Sprite2D.GetVelocity(_ball.Speed, _ball.Heading) + new Vector2(nudge, 0f);
            (_ball.Speed, _ball.Heading) = Sprite2D.GetSpeedAndHeading(v);
        }

        // Drain detection — purely positional so the drain region
        // doesn't need to participate in the bounce pass.
        if (_ball.Center.Y > _drainY)
        {
            BallInPlay = false;
            Audio.Play(Sounds.Hurt, 0.7f);
            // No score penalty; just reset for next drop.
        }
    }
}
