#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run SpacemanScroller.cs

// Parallax side-scroller proof-of-concept:
// A spaceman walks on a planet surface; left or right and jumps.
// The background moves as the spaceman moves.

using System.Numerics;

using Blitter;
using Blitter.Bits;
using Blitter.Blocks2D;

// Resolve asset files relative to this source file.
Application.Current.SetCallerAssetFolder();

// Logical surface matches the artwork (1920x1080). 
const int LogicalW = 1920;
const int LogicalH = 1080;

// World Y of the bottom edge of every background plate.
const float ImageBottomY = LogicalH / 2f;

// Y coordinate the space man's feet land at.
const float GroundY = 450f;
const float WalkSpeed = 240f;          // world px / sec
const float SpriteScale = 0.5f;
const double WalkFrameSeconds = 0.08;
const double IdleFrameSeconds = 0.25;

// the window to render into
var window = new Window2D
{
    Title = "Moon Scroller — ← → walk, Space jump, Esc quit",
    BackgroundColor = new Color(8, 8, 20),
    CloseKey = Key.Escape,
    //RelativeMouseMode = true,
    FullScreen = true,
    LogicalSize = (LogicalW, LogicalH),
    LogicalPresentation = LogicalPresentation.Letterbox,
};

// the scene to run 
var scene = new Scene2D
{
    Layers =
    [
        // camera layer responsible for giving the scene/renderer a camera.
        new CameraLayer2D(),

        // The background with parallax plates.
        new ParallaxBackground2D
        {
            BottomY = ImageBottomY,
            Plates =
            {
                new() { Image = "01_sky_planet.png", Parallax = Vector2.Zero, RepeatX = false },
                { "02_mountains_far.png",  0.15f },
                { "03_mountains_mid.png",  0.30f },
                { "04_mountains_near.png", 0.60f },
                { "05_crystals_big.png",   1.00f },
                { "06_ground_fg.png",      1.00f },
            },
        },

        // The shadow below the spaceman
        new SpacemanShadowLayer 
        { 
            GroundY = GroundY 
        },

        // The playfield contains any sprites
        new PlayField2D
        {
            Sprites =
            [
                // the walking, jumping spacemen
                new Spaceman
                {
                    Image =
                    {
                        FilePath = "space-man-sprites.png",
                        ["idle-right"] = { Frames = { 0, 1, 2, 3 }, FrameDuration = TimeSpan.FromSeconds(IdleFrameSeconds) },
                        ["idle-left"]  = { Frames = { 0, 1, 2, 3 }, FrameDuration = TimeSpan.FromSeconds(IdleFrameSeconds), Flip = FlipMode.Horizontal },
                        ["walk-right"] = { Frames = { 9, 10, 11, 12, 13, 14, 15, 16 }, FrameDuration = TimeSpan.FromSeconds(WalkFrameSeconds) },
                        ["walk-left"]  = { Frames = { 9, 10, 11, 12, 13, 14, 15, 16 }, FrameDuration = TimeSpan.FromSeconds(WalkFrameSeconds), Flip = FlipMode.Horizontal },
                        ["jump-right"] = { Frames = { 7, 4, 5, 6, 7 }, Duration = TimeSpan.FromSeconds(SpacemanController.JumpAirtime), Loop = AnimationLoop.Once },
                        ["jump-left"]  = { Frames = { 7, 4, 5, 6, 7 }, Duration = TimeSpan.FromSeconds(SpacemanController.JumpAirtime), Loop = AnimationLoop.Once, Flip = FlipMode.Horizontal },
                    },
                    Scale = SpriteScale,
                    WalkSpeed = WalkSpeed,
                    GroundY = GroundY,
                    Behaviors =
                    [
                        // walks and jumps the spaceman using player input
                        new SpacemanController(window.Input),

                        // keeps the camera following along with the spaceman
                        new CameraFollow2D
                        {
                            ViewportSize = new Vector2(LogicalW, LogicalH),
                            MarginFraction = 0.35f,
                            FollowY = false, // side-scroller: only track horizontal motion
                        },
                    ],
                },
            ],
        },
    ]
};

// runs the scene until window close (or other exit condition)
await scene.RunAsync(window);


// ---------------------------- Sprites & Layers ----------------------------

/// <summary>
/// The spaceman sprite.
/// </summary>
public class Spaceman : Sprite2D
{
    public string Facing { get; set; } = "right";
    public TimeSpan? JumpStartedAt { get; set; } = null;
    public float GroundY { get; set; }
    public float FeetOffsetY { get; private set; }
    public float JumpOffsetY { get; set; } = 0f;
    public float WalkSpeed { get; set; }
    public float ShadowWidth { get; private set; }

    protected override void OnAttach(IEntity entity)
    {
        base.OnAttach(entity);
        var standSize = ((ITextureRegion)((AnimatedVisual2D)Image.Visual!).Catalog["idle-right"].Frames[0].Texture).Region;
        FeetOffsetY = standSize.Height * 0.5f * Scale;
        ShadowWidth = standSize.Width * 0.9f * Scale;
        Center = new Vector2(0f, GroundY - FeetOffsetY);
    }
}

/// <summary>
/// The spaceman movement controller.
/// This handles all the spaceman's motion and gravity.
/// </summary>
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

    public override void Apply(in UpdateContext ctx)
    {
        var self = (Sprite2D)this.Entity;
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

/// <summary>
/// The shadow layer for the spaceman.
/// Draws a drop-shadow below the spaceman.
/// </summary>
public class SpacemanShadowLayer : Layer2D
{
    private Spaceman _spaceman = null!;

    /// <summary>World Y the shadow is pinned to (the ground line).</summary>
    public float GroundY { get; set; }

    protected override void OnAttach(IEntity entity)
    {
        base.OnAttach(entity);
        _spaceman = Scene.GetLayer<PlayField2D>().GetSprite<Spaceman>();
    }

    public override void Update(in UpdateContext context) { }

    protected override void DrawContent(Renderer2D rd)
    {
        float airFraction = Math.Min(1f, -_spaceman.JumpOffsetY / 120f);
        float shadowScale = 1f - 0.55f * airFraction;
        byte shadowAlpha = (byte)(110 - 70 * airFraction);
        DrawShadowEllipse(
            rd, 
            _spaceman.Center.X, GroundY + 1f,
            _spaceman.ShadowWidth * 0.5f * shadowScale,
            4f * shadowScale,
            new Color(0, 0, 0, shadowAlpha)
            );
    }

    // Filled ellipse approximated as horizontal strips — 
    // no native ellipse primitive on Renderer2D today.
    private static void DrawShadowEllipse(Renderer2D rd, float cx, float cy, float rx, float ry, Color color)
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
}
