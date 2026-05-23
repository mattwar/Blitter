#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run AnimatedSpaceMan.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Animates the space man across a moonscape using sequences carved
// out of space-man-sprites.png by TextureCatalog.Sense().
//
// Controls:
//   Left / Right    walk (flips horizontally going left)
//   Space           jump (one-shot 7-4-5-6-7 with a small arc)
//   LShift          hold "power stance" (frame 8)
//   Esc             quit

using System.Numerics;

using Blitter;
using Blitter.Bits;

// Window is sized to the moonscape image (990x700) so we never have
// to deal with regions outside it.
const int DesignW = 990;
const int DesignH = 700;

var moonscape = Bitmap.Load(Asset.GetPathRelativeToCaller("moonscape.jpg"));

// Sprite sheet already carries an alpha channel (PNG), so Sense's
// transparency-based gutter detection works directly.
var spacemanSpriteSheet = Bitmap.Load(Asset.GetPathRelativeToCaller("space-man-sprites.png"));
using var spacemanAtlas = TextureCatalog.Sense(
    spacemanSpriteSheet,
    minRegionWidth: 8,
    minRegionHeight: 8,
    minRowGutter: 4,
    minColumnGutter: 4,
    ownsImage: true
    );

// Frame map (matches the indices printed by SenseSpaceMan):
//   0..3   stand (one held pose, replicated by the sheet layout)
//   4,5,6  jump up / mid / down
//   7      land / squat
//   8      power stance
//   9..16  walk cycle (rightward)
const double WalkFrameSeconds = 0.08;
const double IdleFrameSeconds = 0.25;

// Jump physics: launch upward at JumpInitialVelocity, fall back
// under MoonGravity. Lower gravity = longer hang time + higher arc
// for the same launch speed; ~1/5 "earth" gravity at typical
// platformer scales feels moony.
const float JumpInitialVelocity = 280f; // px / sec (positive = up)
const float MoonGravity = 380f;         // px / sec^2
// Airtime: solve y(t) = v0*t - 0.5*g*t^2 = 0 -> t = 2*v0/g.
float JumpAirtime = 2f * JumpInitialVelocity / MoonGravity;
// Pace the 5-frame jump sequence to span the whole hang.
TimeSpan JumpFrameDuration = TimeSpan.FromSeconds(JumpAirtime / 5.0);

// Each motion exists twice: a right-facing sequence over the raw
// atlas frames (sheet faces right by default) and a left-facing
// sequence over the same frames with Spec.Flip = Horizontal so the
// renderer mirrors them at draw time. Treating left/right as
// distinct states means HitShape and rendering automatically agree
// without a separate runtime flip channel.
var spacemanAnimations = spacemanAtlas.ToAnimationCatalog([
    new("idle-right",  [0, 1, 2, 3], TimeSpan.FromSeconds(IdleFrameSeconds)),
    new("idle-left",   [0, 1, 2, 3], TimeSpan.FromSeconds(IdleFrameSeconds), Flip: FlipMode.Horizontal),
    new("walk-right",  [9, 10, 11, 12, 13, 14, 15, 16], TimeSpan.FromSeconds(WalkFrameSeconds)),
    new("walk-left",   [9, 10, 11, 12, 13, 14, 15, 16], TimeSpan.FromSeconds(WalkFrameSeconds), Flip: FlipMode.Horizontal),
    new("jump-right",  [7, 4, 5, 6, 7], JumpFrameDuration, AnimationLoop.Once),
    new("jump-left",   [7, 4, 5, 6, 7], JumpFrameDuration, AnimationLoop.Once, FlipMode.Horizontal),
    new("power-right", [8], TimeSpan.FromSeconds(1)),
    new("power-left",  [8], TimeSpan.FromSeconds(1), Flip: FlipMode.Horizontal),
]);

var visual = new AnimatedVisual2D(spacemanAnimations, initialState: "idle-right");

// Place the man on a horizontal ground line ~2/3 down the moonscape.
const float GroundY = DesignH * (2f / 3f);
const float SpriteScale = 0.5f;
const float WalkSpeed = 160f;          // px / sec

// Footprint of frame 0 — used to lift the sprite center so the feet
// rest on GroundY regardless of sprite scale.
var standSize = ((ITextureRegion)spacemanAtlas[0]).SourceRect;
float feetOffset = standSize.Height * 0.5f * SpriteScale;

float posX = DesignW * 0.5f;
float posY = GroundY - feetOffset;
// Last horizontal input direction, used to pick the *-right or *-left
// sequence variant. Sprite faces right at startup.
string facing = "right";

// Jump bookkeeping: when null, not jumping; otherwise the elapsed
// time the jump began.
TimeSpan? jumpStartedAt = null;

var window = new Window2D(DesignW, DesignH)
{
    Title = "Animated Space Man — ← → walk, Space jump, LShift power",
    BackgroundColor = new Color(8, 8, 20),
    CloseKey = Key.Escape,
};

window.Renderer.SetLogicalSize(DesignW, DesignH, LogicalPresentation.Letterbox);

await window.RunAsync(rd => 
{
    var now = rd.ElapsedSinceStart;
    float dt = rd.ElapsedSecondsSinceLastRender;

    // --- Input -> state machine -------------------------------------
    var input = window.Input;
    bool left = input.IsDown(Key.Left);
    bool right = input.IsDown(Key.Right);
    bool power = input.IsDown(Key.LShift) || input.IsDown(Key.RShift);
    bool jumpPressed = input.WasJustPressed(Key.Space);

    if (jumpPressed && jumpStartedAt is null)
    {
        jumpStartedAt = now;
        visual.State = $"jump-{facing}";
    }

    // Walking moves the sprite even mid-jump (so you can leap
    // forward). Facing only updates when actually walking, so
    // jumping straight up doesn't snap-flip mid-air.
    float move = 0f;
    if (right) { move += 1f; facing = "right"; }
    if (left)  { move -= 1f; facing = "left"; }
    posX += move * WalkSpeed * dt;
    // Constrain to the moonscape (image edges).
    float halfW = standSize.Width * 0.5f * SpriteScale;
    posX = Math.Clamp(posX, halfW, DesignW - halfW);

    // Jump arc + state transition out of jump.
    float yOffset = 0f;
    if (jumpStartedAt is { } start)
    {
        float t = (float)(now - start).TotalSeconds;
        if (t >= JumpAirtime)
        {
            jumpStartedAt = null;
        }
        else
        {
            // y(t) = v0*t - 0.5*g*t^2  (positive = up). Screen Y
            // grows downward, so negate for the draw offset.
            float y = JumpInitialVelocity * t - 0.5f * MoonGravity * t * t;
            yOffset = -y;
        }
    }

    // Pick a non-jump state once the jump finishes (or whenever not jumping).
    if (jumpStartedAt is null)
    {
        if (power)              visual.State = $"power-{facing}";
        else if (left || right) visual.State = $"walk-{facing}";
        else                    visual.State = $"idle-{facing}";
    }

    // --- Draw -------------------------------------------------------
    // Letterbox already clears to BackgroundColor; draw the moonscape
    // filling the design surface 1:1.
    rd.DrawImage(moonscape, new Rect(0, 0, DesignW, DesignH));

    // Drop shadow on the ground under the sprite. Stays anchored at
    // GroundY (doesn't follow yOffset) and shrinks + fades as the
    // sprite gains altitude, selling the jump height.
    float shadowBaseWidth = standSize.Width * SpriteScale * 0.9f;
    float airFraction = Math.Min(1f, -yOffset / 100f);   // 0 = grounded, 1 = near peak
    float shadowScale = 1f - 0.55f * airFraction;
    byte shadowAlpha = (byte)(110 - 70 * airFraction);
    DrawShadowEllipse(rd, posX, GroundY + 1f,
        shadowBaseWidth * 0.5f * shadowScale,
        4f * shadowScale,
        new Color(0, 0, 0, shadowAlpha));

    var pose = new Pose2D(new Vector2(posX, posY + yOffset), 0f, SpriteScale);
    visual.Draw(rd, pose, Color.White, now);

    // HUD
    rd.DrawColor = new Color(220, 230, 245);
    rd.DrawDebugText(12, 12, $"state: {visual.State}");
    rd.DrawDebugText(12, 28, "left/right: walk   space: jump   lshift: power");
});

// Filled ellipse approximated as horizontal strips. No native ellipse
// primitive on Renderer2D today; this is good enough for a soft drop
// shadow and stays cheap (a few dozen DrawFillRect calls per frame).
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
