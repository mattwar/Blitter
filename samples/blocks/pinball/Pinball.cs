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
    CloseKey = Key.Escape,
};
window.Renderer.SetLogicalSize(W, H, LogicalPresentation.Letterbox);

// --- Build the chrome ball ----------------------------------------
// One 64×64 Bitmap drawn once with SKShader.CreateRadialGradient.
// Off-center light source gives the "metallic sphere" look; a tiny
// brighter spec highlight sells it. Outside the disc is fully
// transparent so HitShapeCache infers a tight circular hit shape.
var ballImage = MakeChromeBall(64);

// --- Layers --------------------------------------------------------
var scoreFont   = new Font(["Consolas", "Menlo"], 36, bold: true);
var bannerFont  = new Font(["Consolas", "Menlo"], 96, bold: true);
var popupFont   = new Font(["Consolas", "Menlo"], 28, bold: true);

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

// --- Playfield -----------------------------------------------------
var playField = new PlayField2D
{
    WorldBounds = new Rect(0, 0, W, H),
};

// Outer walls (drain gap left in the middle of the bottom V).
const float WallInset = 16f;
playField.AddBarrier(LineBarrier2D.WallRight(WallInset, 0, H));               // left
playField.AddBarrier(LineBarrier2D.WallLeft (W - WallInset, 0, H));           // right
playField.AddBarrier(LineBarrier2D.Ceiling  (WallInset, W - WallInset, WallInset));

// Bottom V-floor — outer slopes guide the ball to the flipper
// pivots. The drain gap between the flipper tips is what the
// player has to defend.
playField.AddBarrier(LineBarrier2D.Slope(
    new Vector2(WallInset,         820f),
    new Vector2(W / 2f - 95f,      870f),
    solidFreeSide: new Vector2( 1f, -1f)));
playField.AddBarrier(LineBarrier2D.Slope(
    new Vector2(W - WallInset,     820f),
    new Vector2(W / 2f + 95f,      870f),
    solidFreeSide: new Vector2(-1f, -1f)));

// Bumpers — typed Bumper subclass carries its display tint.
// Upper trio: big high-scoring pop-bumpers in the top half of the table.
// Lower pair: smaller "mini" bumpers parked above the slings.
var bumpers = new[]
{
    new Bumper(180f, 280f, 44f, new Color(255,  90, 120)),
    new Bumper(420f, 230f, 44f, new Color( 90, 200, 255)),
    new Bumper(300f, 410f, 50f, new Color(140, 255, 140)),
    new Bumper(140f, 580f, 22f, new Color(255, 200,  90)),
    new Bumper(580f, 580f, 22f, new Color(255, 200,  90)),
};
foreach (var b in bumpers)
    playField.AddBarrier(b);

// Slingshots — angled segments above the V-floor that flick the
// ball back toward the bumpers. solidFreeSide points up-and-inward.
var slingLeft  = new Slingshot(
    new Vector2(WallInset + 8f, 620f),
    new Vector2(220f,           740f),
    solidFreeSide: new Vector2( 1f, -1f));
var slingRight = new Slingshot(
    new Vector2(W - WallInset - 8f, 620f),
    new Vector2(500f,               740f),
    solidFreeSide: new Vector2(-1f, -1f));
playField.AddBarrier(slingLeft);
playField.AddBarrier(slingRight);

// Flippers. Pivots sit just above the drain at the inner ends of
// the V-floor. At rest both flippers point down-and-inward; on
// Pressed they swing inward-and-up, kicking any ball at the tip
// upward via the bounce-with-surface-velocity path in
// BounceAtBarrier2D.
var flipperLeft = new FlipperBarrier2D
{
    Pivot          = new Vector2(W / 2f - 95f, 870f),
    Length         = 90f,
    Radius         = 10f,
    RestAngleDeg   =  28f,   // down-right
    ActiveAngleDeg = -22f,   // up-right
    SnapDegPerSec  = 1900f,
};
var flipperRight = new FlipperBarrier2D
{
    Pivot          = new Vector2(W / 2f + 95f, 870f),
    Length         = 90f,
    Radius         = 10f,
    RestAngleDeg   = 180f - 28f,  // down-left
    ActiveAngleDeg = 180f + 22f,  // up-left
    SnapDegPerSec  = 1900f,
};
playField.AddBarrier(flipperLeft);
playField.AddBarrier(flipperRight);

// --- Ball ----------------------------------------------------------
var ball = new Pinball
{
    Visual = ballImage,
    Center = plungerSpawn,
    Scale = (BallRadius * 2f) / ballImage.Width,
};

ball.Behaviors.Add(new Gravity2D { Acceleration = new Vector2(0f, 1400f), MaxFallSpeed = 1600f });
ball.Behaviors.Add(new Motion2D());

// Camera shake — declared early because OnBounce closes over it.
// Runs against the renderer camera so bumper hits jolt the whole table.
// Position the camera at the table center so world coords map 1:1 to
// screen coords (the default camera position is (0,0), which would
// shift the table off-screen by (W/2, H/2)).
var camera = new Camera2D { Position = new Vector2(W / 2f, H / 2f) };
window.Renderer.Camera = camera;
var shake = new CameraShake2D { Camera = camera, MaxOffset = 14f, Decay = 1.8f };

var bounce = new BounceAtBarrier2D
{
    Restitution       = 0.82f,
    TangentialDamping = 0.985f,
};

bounce.OnBounce = (self, barrier, normal) =>
{
    switch (barrier)
    {
        case Bumper bumper:
        {
            // Kick: add an extra outward impulse so bumpers feel alive instead of just elastic.
            const float kick = 320f;
            var v = Sprite2D.GetVelocity(self.Speed, self.Heading) + normal * kick;
            (self.Speed, self.Heading) = Sprite2D.GetSpeedAndHeading(v);

            scoreboard.PositivePopupColor = bumper.Tint;
            scoreboard.Add(100, self.Center);

            shake.AddTrauma(0.55f);
            Audio.Play(Sounds.Coin, 0.6f);
            break;
        }
        case Slingshot:
        {
            var v = Sprite2D.GetVelocity(self.Speed, self.Heading) + normal * 180f;
            (self.Speed, self.Heading) = Sprite2D.GetSpeedAndHeading(v);
            scoreboard.PositivePopupColor = new Color(200, 200, 255);
            scoreboard.Add(25, self.Center);
            shake.AddTrauma(0.2f);
            Audio.Play(Sounds.Bounce, 0.7f);
            break;
        }
    }
};
ball.Behaviors.Add(bounce);

// Plunger + drain controller. Lives on the ball so it can run every
// frame regardless of which barrier was hit.
var plunger = new PlungerController(window.Input, plungerSpawn, DrainY, scoreboard);
ball.Behaviors.Add(plunger);

// Shake runs after the others so it doesn't get clobbered by camera
// follow (we don't actually use follow here, but ordering still
// matters if it's added later).
ball.Behaviors.Add(shake);

playField.AddSprite(ball);

// --- Table decor ---------------------------------------------------
// Barriers are pure collision geometry — they don't render. This
// layer walks playField.Barriers each frame and draws each one:
// thick colored line segments for LineBarrier2Ds, glowing discs for
// CircleBarrier2Ds. Sits behind the playfield so the ball draws on
// top.
var tableDecor = new CustomLayer2D
{
    OnRender = rd =>
    {
        // Faint floor band at the drain so the player sees where the
        // ball gets lost.
        rd.DrawColor = new Color(70, 20, 30);
        rd.DrawFillRect(new Rect(0, DrainY, W, H - DrainY));

        foreach (var barrier in playField.Barriers)
        {
            switch (barrier)
            {
                case Bumper bumper:
                    DrawBumper(rd, bumper.Center, bumper.Radius, bumper.Tint);
                    break;
                case Slingshot sling:
                    DrawThickLine(rd, sling.Start, sling.End, new Color(255, 150, 80), 5f);
                    break;
                case FlipperBarrier2D flipper:
                {
                    var tint = flipper.Pressed
                        ? new Color(255, 230, 120)
                        : new Color(220, 220, 235);
                    DrawThickLine(rd, flipper.Pivot, flipper.Tip, tint, flipper.Radius * 2f);
                    DrawBumper(rd, flipper.Pivot, flipper.Radius + 2f, new Color(180, 180, 200));
                    break;
                }
                case LineBarrier2D line:
                    DrawThickLine(rd, line.Start, line.End, new Color(120, 170, 230), 5f);
                    break;
                case CircleBarrier2D disc:
                    DrawBumper(rd, disc.Center, disc.Radius, new Color(255, 215, 0));
                    break;
            }
        }
    },
};

// --- HUD overlay ---------------------------------------------------
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

// --- Scene ---------------------------------------------------------
var scene = new Scene2D
{
    Layers = { tableDecor, playField, popups, scoreboard, hud },
    Behaviors =
    {
        new CustomSceneBehavior2D
        {
            OnApply = (s, in ctx) =>
            {
                // Flippers: each frame, drive Pressed off the shift
                // keys. The barriers handle slewing + surface velocity
                // themselves.
                flipperLeft.Pressed  = window.Input.IsDown(Key.LShift);
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

await scene.RunAsync(window);

// --- Helpers -------------------------------------------------------

static void DrawThickLine(Renderer2D rd, Vector2 a, Vector2 b, Color color, float thickness)
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

static void DrawBumper(Renderer2D rd, Vector2 center, float radius, Color color)
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

// Typed bumper barrier — Tint carries the display color so OnBounce
// and the table-decor layer don't have to round-trip through an
// untyped UserData slot.
sealed class Bumper : CircleBarrier2D
{
    public Color Tint { get; }

    public Bumper(float x, float y, float radius, Color tint) : base(x, y, radius)
    {
        Tint = tint;
    }
}

// Typed slingshot — distinguishes a rubber kicker from a plain wall
// so the bounce handler can pay it different scores and sounds.
sealed class Slingshot : LineBarrier2D
{
    public Slingshot(Vector2 start, Vector2 end, Vector2 solidFreeSide)
        : base(start, end, ChooseNormal(start, end, solidFreeSide))
    {
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
