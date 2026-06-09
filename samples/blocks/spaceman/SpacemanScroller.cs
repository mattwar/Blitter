#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run MoonScroller.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Parallax side-scroller proof-of-concept: a stack of six tileable
// background plates scrolls past at different speeds while the
// space man walks across them. Exercises Scene2D + Layer2D
// composition, the new RepeatingImageLayer2D block, and
// CameraFollow2D for horizontal tracking with no world bounds (the
// world scrolls forever).

using System.Numerics;

using Blitter;
using Blitter.Bits;
using Blitter.Blocks2D;

// Logical surface matches the artwork (1920x1080). 
// Window is half that size and letterboxes so it scales cleanly.
const int LogicalW = 1920;
const int LogicalH = 1080;
const int WindowW = 1280;
const int WindowH = 720;

// World Y of the bottom edge of every background plate.
// LogicalH/2 anchors the plates so they exactly fill the viewport
// vertically when the camera Y is 0.
const float ImageBottomY = LogicalH / 2f;

// Y coordinate the space man's feet land at.
const float GroundY = 450f;
const float WalkSpeed = 240f;          // world px / sec
const float SpriteScale = 0.5f;


var window = new Window2D(WindowW, WindowH)
{
    Title = "Moon Scroller — ← → walk, Space jump, Esc quit",
    BackgroundColor = new Color(8, 8, 20),
    CloseKey = Key.Escape,
};

window.Renderer.SetLogicalSize(LogicalW, LogicalH, LogicalPresentation.Letterbox);

// Resolve loose asset files next to this source file.
Application.Current.SetCallerAssetFolder();

// --- Background plates --------------------------------------------
// Loaded in back-to-front order. ParallaxFactor controls how fast
// each layer scrolls relative to the camera: 0 = locked, 1 = matches
// the playfield, anything in between drifts behind.
var skyImage      = Bitmap.Load("01_sky_planet.png");
var farMountains  = Bitmap.Load("02_mountains_far.png");
var midMountains  = Bitmap.Load("03_mountains_mid.png");
var nearMountains = Bitmap.Load("04_mountains_near.png");
var crystals      = Bitmap.Load("05_crystals_big.png");
var groundFg      = Bitmap.Load("06_ground_fg.png");

// spaceman sprite sheet
const double WalkFrameSeconds = 0.08;
const double IdleFrameSeconds = 0.25;
// Pace the 5-frame [7,4,5,6,7] jump (squat → up → mid → down →
// squat) to span the full airtime so the landing pose lands with the
// feet, not above the peak.
var jumpFrameDuration = TimeSpan.FromSeconds(SpacemanController.JumpAirtime / 5.0);

// The sheet's sprites are not on a uniform grid, so we leave TileSize
// unset and let ImageSource auto-sense the cells: with no TileSize an
// integer frame is the Nth detected region. Frames are listed by index
// — the same numbers the old hand-built catalog used — and each state
// inherits the shared sheet, so the whole animation table is declared
// in one place. The first declared state ("idle-right") is the initial one.
var spaceman = new Spaceman
{
    Image =
    {
        FilePath = "space-man-sprites.png",
        ["idle-right"] = { Frames = { 0, 1, 2, 3 }, FrameDuration = TimeSpan.FromSeconds(IdleFrameSeconds) },
        ["idle-left"]  = { Frames = { 0, 1, 2, 3 }, FrameDuration = TimeSpan.FromSeconds(IdleFrameSeconds), Flip = FlipMode.Horizontal },
        ["walk-right"] = { Frames = { 9, 10, 11, 12, 13, 14, 15, 16 }, FrameDuration = TimeSpan.FromSeconds(WalkFrameSeconds) },
        ["walk-left"]  = { Frames = { 9, 10, 11, 12, 13, 14, 15, 16 }, FrameDuration = TimeSpan.FromSeconds(WalkFrameSeconds), Flip = FlipMode.Horizontal },
        ["jump-right"] = { Frames = { 7, 4, 5, 6, 7 }, FrameDuration = jumpFrameDuration, Loop = AnimationLoop.Once },
        ["jump-left"]  = { Frames = { 7, 4, 5, 6, 7 }, FrameDuration = jumpFrameDuration, Loop = AnimationLoop.Once, Flip = FlipMode.Horizontal },
    },
    Scale = SpriteScale,
    WalkSpeed = WalkSpeed,
    GroundY = GroundY,
};

// Measure the standing pose (idle frame 0) to anchor the feet and shadow.
var visual = (AnimatedVisual2D)spaceman.Image.Visual!;
var standSize = ((ITextureRegion)visual.Catalog["idle-right"].Frames[0].Texture).Region;
float feetOffset = standSize.Height * 0.5f * SpriteScale;
spaceman.FeetOffsetY = feetOffset;
spaceman.Center = new Vector2(0f, GroundY - feetOffset);

spaceman.Behaviors.Add(
    new SpacemanController(window.Input)
    );

// Camera follows the space man horizontally; no world bounds means
// the level scrolls forever in either direction. Y stays at 0 so
// the background plates (anchored to image-bottom = world Y 540)
// always fill the viewport regardless of where the man stands.
var camera = new Camera2D { Position = new Vector2(spaceman.Center.X, 0f) };
window.Renderer.Camera = camera;

spaceman.Behaviors.Add(
    new CameraFollow2D
    {
        Camera = camera,
        ViewportSize = new Vector2(LogicalW, LogicalH),
        MarginFraction = 0.35f,
        FollowY = false, // side-scroller: only track horizontal motion
    });

var playfield = new PlayField2D();
playfield.AddSprite(spaceman);

// Drop-shadow layer drawn just before the playfield (so the sprite
// renders on top). Anchored at GroundY, not the sprite's Y, so it
// stays on the ground while the man arcs through the air; shrinks
// and fades as altitude grows to sell the jump height.
float shadowBaseWidth = standSize.Width * SpriteScale * 0.9f;
var shadowLayer = new CustomLayer2D
{
    ParallaxFactor = Vector2.One,
    OnRender = rd =>
    {
        float airFraction = Math.Min(1f, - spaceman.JumpOffsetY / 120f);
        float shadowScale = 1f - 0.55f * airFraction;
        byte shadowAlpha = (byte)(110 - 70 * airFraction);
        DrawShadowEllipse(
            rd, spaceman.Center.X, GroundY + 1f,
            shadowBaseWidth * 0.5f * shadowScale,
            4f * shadowScale,
            new Color(0, 0, 0, shadowAlpha)
            );
    }
};

// compose scene with background layers with parallax so they scroll at different speeds

var skyLayer = new RepeatingImageLayer2D(skyImage)
{
    BottomY = ImageBottomY,
    OffsetX = -skyImage.Width / 2f,
    RepeatX = false,
    ParallaxFactor = Vector2.Zero,
};

RepeatingImageLayer2D TiledLayer(Texture2D img, float parallax) =>
    new(img)
    {
        BottomY = ImageBottomY,
        ParallaxFactor = new Vector2(parallax, 0f),
    };

var scene = new Scene2D
{
    Layers =
    {
        skyLayer,
        TiledLayer(farMountains,  0.15f),
        TiledLayer(midMountains,  0.30f),
        TiledLayer(nearMountains, 0.60f),
        TiledLayer(crystals,      1.00f),
        TiledLayer(groundFg,      1.00f),
        shadowLayer,
        playfield,
    }
};

await scene.RunAsync(window);

// Filled ellipse approximated as horizontal strips — no native
// ellipse primitive on Renderer2D today.
static void DrawShadowEllipse(Renderer2D rd, float cx, float cy, float rx, float ry, Color color)
{
    if (rx <= 0f || ry <= 0f) return;
    rd.DrawColor = color;
    int steps = Math.Max(1, (int)MathF.Ceiling(ry));
    for (int i = -steps; i <= steps; i++)
    {
        float t = i / (float)steps;
        float w = rx * MathF.Sqrt(Math.Max(0f, 1f - t * t));
        rd.DrawFillRect(new Rect(cx - w, cy + i, 2f * w, 1f));
    }
}

public class Spaceman : Sprite2D
{
    public string Facing { get; set; } = "right";
    public TimeSpan? JumpStartedAt { get; set; } = null;
    public float GroundY { get; set; }
    public float FeetOffsetY { get; set; }
    public float JumpOffsetY { get; set; } = 0f;
    public float WalkSpeed { get; set; }
}

public class SpacemanController : SpriteBehavior2D
{
    private readonly FrameInput input;

    // Jump physics: launch upward at JumpInitialVelocity, fall back
    // under MoonGravity. Lower gravity = longer hang time + higher arc
    // for the same launch speed.
    public const float JumpInitialVelocity = 360f; // px / sec (positive = up)
    public const float MoonGravity = 480f;         // px / sec^2

    /// <summary>Total time in the air for one jump, in seconds.</summary>
    public const float JumpAirtime = 2f * JumpInitialVelocity / MoonGravity;

    public SpacemanController(FrameInput input)
    {
        this.input = input;
    }

    public override void Apply(Sprite2D self, in UpdateContext2D ctx)
    {
        if (self is not Spaceman spaceman) 
            return;

        bool left  = input.IsDown(Key.Left);
        bool right = input.IsDown(Key.Right);
        bool jumpPressed = input.WasJustPressed(Key.Space);
        if (right) spaceman.Facing = "right";
        if (left)  spaceman.Facing = "left";
        float move = (right ? 1f : 0f) - (left ? 1f : 0f);

        if (jumpPressed && spaceman.JumpStartedAt is null)
            spaceman.JumpStartedAt = ctx.ElapsedSinceStart;

        // Jump arc: y(t) = v0*t - 0.5*g*t^2 (positive = up).
        // Screen Y grows downward, so negate for the draw offset.
        spaceman.JumpOffsetY = 0f;
        if (spaceman.JumpStartedAt is { } start)
        {
            float t = (float)(ctx.ElapsedSinceStart - start).TotalSeconds;
            if (t >= JumpAirtime)
            {
                spaceman.JumpStartedAt = null;
            }
            else
            {
                float y = JumpInitialVelocity * t - 0.5f * MoonGravity * t * t;
                spaceman.JumpOffsetY = -y;
            }
        }

        float dt = (float)ctx.ElapsedSinceLastUpdate.TotalSeconds;
        var c = self.Center;
        c.X += move * spaceman.WalkSpeed * dt;
        c.Y = spaceman.GroundY - spaceman.FeetOffsetY + spaceman.JumpOffsetY;
        self.Center = c;

        string motion = spaceman.JumpStartedAt is not null
            ? "jump"
            : (move != 0f ? "walk" : "idle");

        self.Image.Visual!.State = motion + "-" + spaceman.Facing;
    }
}
