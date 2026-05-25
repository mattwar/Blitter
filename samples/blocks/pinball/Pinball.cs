#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run Pinball.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// pinball: a chrome ball drops from the top of the cabinet, 
// tumbles through a field of circular bumpers and angled
// slingshots, and drains through a gap in the bottom V-floor.

using System.Numerics;

using Blitter;
using Blitter.Bits;
using Blitter.Blocks;

using SkiaSharp;

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
    Title = "Pinball — Space drop, Shift flippers, Esc quit",
    BackgroundColor = new Color(8, 12, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Renderer.SetLogicalSize(W, H, LogicalPresentation.Letterbox);

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

var shake = new CameraShake2D { Camera = camera, MaxOffset = 14f, Decay = 1.8f };

var playField = new PlayField2D
{
    WorldBounds = new Rect(0, 0, W, H),
};

// Outer walls (drain gap left in the middle of the bottom V).
const float WallInset = 16f;

// Side walls
playField.AddBarrier(
    new Wall(
        new Vector2(WallInset, WallInset), 
        new Vector2(WallInset, 820f),     
        new Vector2( 1f, 0f)
        )); // left

playField.AddBarrier(
    new Wall(
        new Vector2(W - WallInset, WallInset), 
        new Vector2(W - WallInset, 820f), 
        new Vector2(-1f, 0f)
        )); // right

playField.AddBarrier(
    new Wall(
        new Vector2(WallInset, WallInset),     
        new Vector2(W - WallInset, WallInset), 
        new Vector2(0f, 1f)
        )); // ceiling

// Bottom V-floor
playField.AddBarrier(new Wall(
    new Vector2(WallInset,    820f),
    new Vector2(W / 2f - 95f, 860f),
    solidFreeSide: new Vector2( 1f, -1f)
    ));

playField.AddBarrier(new Wall(
    new Vector2(W - WallInset, 820f),
    new Vector2(W / 2f + 95f,  860f),
    solidFreeSide: new Vector2(-1f, -1f)
    ));

var bumperSound = Sound.Load(Asset.GetPathRelativeToCaller("bumper.wav"));
var flipperSound = Sound.Load(Asset.GetPathRelativeToCaller("flipper.wav"));
var slingshotSound = Sound.Load(Asset.GetPathRelativeToCaller("slingshot.wav"));

// Bumpers. Each pre-synthesizes its own hit sound at a distinct
// pitch so a multi-bumper combo plays as a little arpeggio.
// Bigger bumpers get lower notes.
var bumpers = new[]
{
    new Bumper(180f, 280f, 44f, new Color(255,  90, 120), bumperSound, scoreboard, shake),
    new Bumper(420f, 230f, 44f, new Color( 90, 200, 255), bumperSound, scoreboard, shake),
    new Bumper(300f, 410f, 50f, new Color(140, 255, 140), bumperSound, scoreboard, shake),
    new Bumper(140f, 580f, 22f, new Color(255, 200,  90), bumperSound, scoreboard, shake),
    new Bumper(580f, 580f, 22f, new Color(255, 200,  90), bumperSound, scoreboard, shake),
};

playField.AddBarriers(bumpers);

// Slingshots
var slingLeft  = new Slingshot(
    new Vector2(WallInset + 8f, 620f),
    new Vector2(220f,740f),
    solidFreeSide: new Vector2( 1f, -1f),
    scoreboard, 
    shake
    )
{
    HitSound = slingshotSound,
};

var slingRight = new Slingshot(
    new Vector2(W - WallInset - 8f, 620f),
    new Vector2(500f, 740f),
    solidFreeSide: new Vector2(-1f, -1f),
    scoreboard, 
    shake
    )
{
    HitSound = slingshotSound,
};

playField.AddBarrier(slingLeft);
playField.AddBarrier(slingRight);

// Flippers
var flipperLeft = new Flipper
{
    HitSound = flipperSound,
    Pivot = new Vector2(W / 2f - 95f, 870f),
    Length = 90f,
    Radius = 10f,
    RestAngleDeg =  28f,   // down-right
    ActiveAngleDeg = -22f,   // up-right
    SnapDegPerSec = 900f,
};

var flipperRight = new Flipper
{
    HitSound = flipperSound,
    Pivot = new Vector2(W / 2f + 95f, 870f),
    Length = 90f,
    Radius = 10f,
    RestAngleDeg = 180f - 28f,  // down-left
    ActiveAngleDeg = 180f + 22f,  // up-left
    SnapDegPerSec = 1900f,
};

playField.AddBarrier(flipperLeft);
playField.AddBarrier(flipperRight);

// The "ball"
var ball = new Pinball
{
    Visual = ballImage,
    Center = plungerSpawn,
    Scale = (BallRadius * 2f) / ballImage.Width,
};

ball.Behaviors.Add(new Gravity2D { Acceleration = new Vector2(0f, 1400f), MaxFallSpeed = 1600f });
ball.Behaviors.Add(new Motion2D());

// Bounce physics is normal barrier bouncing
ball.Behaviors.Add(
    new BarrierBounce2D
    {
        Restitution = 0.82f,
        TangentialDamping = 0.985f,
    });

// Plunger + drain controller.
var plunger = new PlungerController(window.Input, plungerSpawn, DrainY, scoreboard);
ball.Behaviors.Add(plunger);

// Shake runs after the others so it doesn't get clobbered by camera follow
ball.Behaviors.Add(shake);

playField.AddSprite(ball);

// The drain isn't an actual barrier
// we just draw it in its own layer, and let the ball fall through
var drainBand = new CustomLayer2D
{
    OnRender = rd =>
    {
        rd.DrawColor = new Color(70, 20, 30);
        rd.DrawFillRect(new Rect(WallInset, DrainY, W - 2f * WallInset, H - DrainY));
    },
};

// The HUD with score and other text
var hud = new CustomLayer2D
{
    OnRender = rd =>
    {
        using var _ = rd.PushState();
        rd.Camera = null;

        var status = plunger.BallInPlay
            ? $"BALL {plunger.BallNumber}"
            : "SPACE TO DROP";
        scoreFont.DrawText(rd, status, Color.White, 20f, 64f);

        // Subtle hint line for the keys.
        rd.DrawColor = new Color(180, 200, 230);
        rd.DrawDebugText(20, H - 28, "Shift flippers   <- -> nudge   Space drop   Esc quit", scale: 1.5f);
    },
};

// The scene puts it all togther.
var scene = new Scene2D
{
    Layers = 
    { 
        drainBand, 
        playField, 
        popups, 
        scoreboard, 
        hud 
    },
    Behaviors =
    {
        new CustomSceneBehavior2D
        {
            OnApply = (s, in ctx) =>
            {
                // Flippers: each frame, drive Pressed off the shift
                // keys. The barriers handle slewing + surface velocity
                // themselves.
                flipperLeft.Pressed = window.Input.IsDown(Key.LShift);
                flipperRight.Pressed = window.Input.IsDown(Key.RShift);

                // Nudge: tap arrows to give the ball a small lateral
                // impulse. Real pinball tilt warning is out of scope.
                if (!plunger.BallInPlay) return;
                const float nudge = 90f;
                if (window.Input.WasJustPressed(Key.Left))
                {
                    var v = Sprite2D.GetVelocity(ball.Speed, ball.Heading) + new Vector2(-nudge, 0f);
                    (ball.Speed, ball.Heading) = Sprite2D.GetSpeedAndHeading(v);
                }
                if (window.Input.WasJustPressed(Key.Right))
                {
                    var v = Sprite2D.GetVelocity(ball.Speed, ball.Heading) + new Vector2( nudge, 0f);
                    (ball.Speed, ball.Heading) = Sprite2D.GetSpeedAndHeading(v);
                }
            }
        },
    },
};

// Run the scene (makes the game run within the window)
await scene.RunAsync(window);

Console.WriteLine($"Final Score: {scoreboard.Score}");


//--------------------------------------------------------------------------------------------------------------------------
// pure types and helpers below here

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

        // Tiny specular highlight — a small, mostly opaque white blob
        // up-and-left of center.
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

        // Faint reflected-light arc on the lower-right rim — sells the
        // sphere as a real metal ball by hinting at ambient bounce.
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

// A simple pinball: a Sprite2D subclass mostly for typing — its real
// personality is in the behaviors added at the call site.
sealed class Pinball : Sprite2D
{
}

// Shared geometry helpers used by the barrier Draw overrides below.
static class TableDraw
{
    public static void ThickLine(Renderer2D rd, Vector2 a, Vector2 b, Color color, float thickness)
    {
        var d = b - a;
        var len = d.Length();
        if (len <= float.Epsilon) return;
        var n = new Vector2(-d.Y, d.X) / len;
        var h = thickness * 0.5f;
        Span<Vertex2D> verts =
        [
            new(a + n * h, color),
            new(b + n * h, color),
            new(b - n * h, color),
            new(a - n * h, color),
        ];
        Span<int> idx = [0, 1, 2, 0, 2, 3];
        rd.DrawGeometry(verts, idx);
    }

    public static void Disc(Renderer2D rd, Vector2 center, float radius, Color color)
    {
        const int Segs = 36;
        // Highlight center with a brightened color, rim with the
        // saturated color. DrawGeometry interpolates per-vertex colors
        // across the triangle fan to get a soft cap-shaped gradient.
        var hi = new Color(
            (byte)Math.Min(255, color.R + 80),
            (byte)Math.Min(255, color.G + 80),
            (byte)Math.Min(255, color.B + 80));

        Span<Vertex2D> verts = stackalloc Vertex2D[Segs + 1];
        verts[0] = new Vertex2D(center, hi);
        for (int i = 0; i < Segs; i++)
        {
            var theta = i * (MathF.PI * 2f / Segs);
            var p = center + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * radius;
            verts[i + 1] = new Vertex2D(p, color);
        }
        Span<int> idx = stackalloc int[Segs * 3];
        for (int i = 0; i < Segs; i++)
        {
            idx[i * 3] = 0;
            idx[i * 3 + 1] = 1 + i;
            idx[i * 3 + 2] = 1 + ((i + 1) % Segs);
        }
        rd.DrawGeometry(verts, idx);
    }
}

// Generic outer-wall / floor / ceiling segment.
sealed class Wall : LineBarrier2D
{
    public Wall(Vector2 start, Vector2 end, Vector2 solidFreeSide)
        : base(start, end, solidFreeSide)
    {
        Material = BarrierMaterial.Metal;
    }

    public override void Draw(Renderer2D renderer)
    {
        TableDraw.ThickLine(renderer, Start, End, new Color(120, 170, 230), 5f);
    }
}

// Bumper
sealed class Bumper : CircleBarrier2D
{
    private readonly ScoreLayer2D _score;
    private readonly CameraShake2D _shake;
    private readonly Sound _hitSound;

    public Color Tint { get; }

    public Bumper(float x, float y, float radius, Color tint, Sound sound, ScoreLayer2D score, CameraShake2D shake)
        : base(x, y, radius)
    {
        Tint = tint;
        _score = score;
        _shake = shake;
        _hitSound = sound;
        Material = new BarrierMaterial(Restitution: 0.95f, Friction: 0.05f, KickSpeed: 320f);
    }

    public override void Draw(Renderer2D renderer)
    {
        TableDraw.Disc(renderer, Center, Radius, Tint);
    }

    public override void OnHitSprite(Sprite2D hitter, in UpdateContext2D context)
    {
        _score.PositivePopupColor = Tint;
        _score.Add(100, hitter.Center);
        _shake.AddTrauma(0.55f);
        Audio.Play(_hitSound, 0.6f);
    }
}

// Typed slingshot — a rubber kicker. Same idea as Bumper: owns its
// visual and its hit reaction. The line normal precomputed by
// LineBarrier2D is the outward kick direction.
sealed class Slingshot : LineBarrier2D
{
    private readonly ScoreLayer2D _score;
    private readonly CameraShake2D _shake;

    public Slingshot(Vector2 start, Vector2 end, Vector2 solidFreeSide, ScoreLayer2D score, CameraShake2D shake)
        : base(start, end, ChooseNormal(start, end, solidFreeSide))
    {
        _score = score;
        _shake = shake;
        Material = new BarrierMaterial(Restitution: 0.95f, Friction: 0.05f, KickSpeed: 180f);
    }

    public Sound? HitSound { get; set; }

    public override void Draw(Renderer2D renderer)
    {
        TableDraw.ThickLine(renderer, Start, End, new Color(255, 150, 80), 5f);
    }

    public override void OnHitSprite(Sprite2D hitter, in UpdateContext2D context)
    {
        _score.PositivePopupColor = new Color(200, 200, 255);
        _score.Add(25, hitter.Center);
        _shake.AddTrauma(0.2f);
        var sound = this.HitSound;
        if (sound != null)
            Audio.Play(sound, 0.7f);
    }

    private static Vector2 ChooseNormal(Vector2 start, Vector2 end, Vector2 solidFreeSide)
    {
        var d = end - start;
        var perp = new Vector2(-d.Y, d.X);
        if (Vector2.Dot(perp, solidFreeSide) < 0f)
            perp = -perp;
        return perp;
    }
}

// Typed flipper — inherits the capsule physics + slewing from
// SwingArmBarrier2D and adds a self-drawn visual: a chunky capsule
// from pivot to tip plus a small pivot disc. The tint shifts gold
// while Pressed for visual feedback.
sealed class Flipper : SwingArmBarrier2D
{
    public Sound? HitSound { get; set;}

    protected override void OnPressed(in UpdateContext2D context)
    {
        var sound = this.HitSound;
        if (sound != null)
            Audio.Play(sound, 0.5f);
    }

    public override void Draw(Renderer2D renderer)
    {
        var tint = Pressed
            ? new Color(255, 230, 120)
            : new Color(220, 220, 235);
        TableDraw.ThickLine(renderer, Pivot, Tip, tint, Radius * 2f);
        TableDraw.Disc(renderer, Pivot, Radius + 2f, new Color(180, 180, 200));       
    }
}

// Launches the ball on Space, watches for drain, and respawns. Holds
// the current ball number so the HUD can read it.
sealed class PlungerController : SpriteBehavior2D
{
    private readonly FrameInput _input;
    private readonly Vector2 _spawn;
    private readonly float _drainY;
    private readonly ScoreLayer2D _score;

    public PlungerController(FrameInput input, Vector2 spawn, float drainY, ScoreLayer2D score)
    {
        _input = input;
        _spawn = spawn;
        _drainY = drainY;
        _score = score;
        BallInPlay = false;
        BallNumber = 0;
    }

    /// <summary>True between launch and drain.</summary>
    public bool BallInPlay { get; private set; }

    /// <summary>Increments on each launch; lets the HUD show "BALL 3".</summary>
    public int BallNumber { get; private set; }

    private readonly Random _rng = new();

    public override void Apply(Sprite2D target, in UpdateContext2D context)
    {
        if (!BallInPlay)
        {
            // Park the ball at the drop point and freeze it. Space
            // drops it with a small random horizontal kick so each
            // ball plays differently.
            target.Center = _spawn;
            target.Speed = 0f;
            if (_input.WasJustPressed(Key.Space))
            {
                BallInPlay = true;
                BallNumber++;
                // Heading: 180 = straight down. ±25° gives some lateral
                // entry so the ball doesn't fall through the same gap
                // every time.
                target.Heading = 180f + ((float)_rng.NextDouble() - 0.5f) * 50f;
                target.Speed = 380f;
                Audio.Play(Sounds.Jump, 0.7f);
            }
            return;
        }

        // Drain detection — purely positional so the drain region
        // doesn't need to participate in the bounce pass.
        if (target.Center.Y > _drainY)
        {
            BallInPlay = false;
            Audio.Play(Sounds.Hurt, 0.7f);
            // No score penalty; just reset for next drop.
        }
    }
}
