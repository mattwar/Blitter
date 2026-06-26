#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run RicochetRocket.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Blitter;
using Blitter.Bits;
using Blitter.Blocks;
using Blitter.Blocks2D;
using SkiaSharp;

// Resolve loose asset files next to this source file.
Application.Current.SetCallerAssetFolder();

// Fixed design surface. The renderer letterboxes this into whatever
// the actual window size is, so the playfield stays a constant
// 1920x1080 regardless of monitor resolution or fullscreen toggles. 
const int DesignW = 1920;
const int DesignH = 1080;

// World is larger than the visible viewport; the camera scrolls to
// keep the rocket inside a visible zone, and the playfield bounces both
// the rocket and asteroids off the world's outer wall.
const int WorldW = 3840;
const int WorldH = 2160;

var window = new Window2D(DesignW, DesignH)
{
    Title = "Ricochet Rocket",
    BackgroundColor = new Color(0, 0, 20, 0),
    FullScreen = true,
    RelativeMouseMode = true, // hides mouse
    CloseKey = Key.Escape,
    LogicalSize = (DesignW, DesignH),
};

// Font for floating score popups and HUD score readout.
var scoreFont = new Font(["Consolas", "Menlo"], 48, bold: true);
var scoreTint = new Color(255, 215, 0); // gold

// Big font for the end-of-game banner.
var bannerFont = new Font(["Consolas", "Menlo"], 160, bold: true);

// HUD score readout + drift-and-fade popups for points awarded.
var popups = new FloatingTextLayer2D
{
    Font = scoreFont,
    DefaultLifetime = TimeSpan.FromSeconds(1.2),
    DefaultVelocity = new Vector2(0f, -90f),
};

var scoreboard = new ScoreLayer2D
{
    Font = scoreFont,
    Anchor = HudAnchor.TopLeft,
    Offset = new Vector2(20f, 60f),
    Color = scoreTint,
    Popups = popups,
    PositivePopupColor = scoreTint,
    NegativePopupColor = new Color(120, 255, 120),
};

var gameOverDuration = TimeSpan.FromSeconds(3);


// create meteor field - small static obstacles for the rocket to hit
var asteroidImage = Bitmap.Load("asteroid.png");
var asteroids = CreateAsteroidField(20, asteroidImage);

// Debris bursts when the smallest asteroids are destroyed. Tinted
// by AsteriodKind so gold and radioactive rocks leave a recognizable
// colored puff.
var debrisParticles = new ParticleLayer2D(capacity: 1024)
{
    Drag = 1.2f,
};
Asteroid.Particles = debrisParticles;

var rocket = new Rocket
{
    Image = "rocket.png",
    Flame = "flame.png",
    Center = new Vector2(WorldW / 2f, WorldH / 2f),
    Scale = 0.1f,
    Speed = 600f,
    Heading = 45f,
    Behaviors = 
    [ 
        new RocketController(),
        new AsteroidSmasher(scoreboard),
        new CameraFollow2D{ ViewportSize = new Vector2(DesignW, DesignH), MarginFraction = 0.3f },
    ]
};

// Camera scrolls the world to keep the rocket in view. Start it on
// the rocket so the first frame isn't a snap from the world origin.
var camera = new Camera2D { Position = rocket.Center };
var attachedCamera = new AttachedCamera2D { Camera = camera };

// Parallax star field. The rocket is buzzing around an asteroid
// field, not zooming between stars, so both layers stay nearly
// screen-locked: the mid layer only drifts a few pixels across the
// whole world. Star bounds are in *layer-local* coordinates (after
// parallax), so for a screen-locked field we want a viewport-sized
// rect centered on the origin.
var farBounds = new Rect(-DesignW / 2f, -DesignH / 2f, DesignW, DesignH);

// Mid layer's virtual camera moves a few dozen pixels with the
// rocket; pad the bounds by that range so stars don't pop at the
// edges.
const float MidParallax = 0.01f;
var midBounds = new Rect(
    -DesignW / 2f - WorldW * MidParallax,
    -DesignH / 2f - WorldH * MidParallax,
    DesignW + 2 * WorldW * MidParallax,
    DesignH + 2 * WorldH * MidParallax
    );

var starsFar = new StarField2D(500, farBounds, seed: 1)
{
    StarColor = new Color(180, 180, 220, 255),
};

var starsMid = new StarField2D(250, midBounds, seed: 2)
{
    Behaviors = [new Parallax2D { Factor = new Vector2(MidParallax, MidParallax) }],
    StarColor = new Color(220, 220, 255, 255),
};

// The playfield includes the asteriods and the rocket.
var playField = new PlayField2D
{
    Entities = [..asteroids, rocket],
    Traits = [new Bounds2D { Rect = new Rect(0, 0, WorldW, WorldH) }],
    Behaviors = [new DrawWorldBounds2D()],
};

// Ends the run when every target is cleared (or V is pressed). The HUD
// layer reads GameOverAt to draw a "GAME OVER" banner during exit-delay.
var levelComplete = new LevelComplete2D { Delay = gameOverDuration };

// HUD overlay: speed readout and the game-over banner.
var hud = new RocketHud
{
    ScoreFont = scoreFont,
    BannerFont = bannerFont,
    DesignSize = new Vector2(DesignW, DesignH),
};

// Overhead minimap so the player can see asteroids beyond the
// viewport. Sized to the upper-right corner; markers are scaled
// by asteroid size and colored by kind.
var minimap = new RocketMinimapLayer
{
    // 16:9 to match the world (3840x2160) so the viewport overlay
    // and asteroid positions aren't stretched.
    ScreenRect = new Rect(DesignW - 340, 20, 320, 180),
    // Slightly translucent so asteroids passing behind the minimap
    // are still readable through the panel.
    BackgroundColor = new Color(0, 0, 0, 100),
    ViewportSize = new Vector2(DesignW, DesignH),
    ViewportColor = new Color(0,0,0,0), // no outline
};

// Debug overlay: outlines the rocket's HitShape in world space so
// we can see exactly where collisions register. Toggle with H.
var hitDebug = new HitDebugLayer
{
};

var scene = new Scene2D
{
    Entities = 
    [ 
        starsFar, 
        starsMid, 
        playField,
        debrisParticles,
        hitDebug,
        popups,
        scoreboard,
        hud,
        minimap,
    ],
    Behaviors =
    [
        attachedCamera,
        levelComplete,
    ],
};

// Run the scene until done
await scene.RunAsync(window);

Console.WriteLine($"Final Score: {scoreboard.Score}");

//---------------------------------------------------------------------------------------------------------------

static List<Asteroid> CreateAsteroidField(int count, Bitmap image)
{
    var asteroids = new List<Asteroid>();
    var rng = Random.Shared;

    var rocketX = WorldW / 2f;
    var rocketY = WorldH / 2f;

    for (int i = 0; i < count; i++)
    {
        // keep meteors away from the rocket's starting position
        float x, y;
        do
        {
            x = rng.Next(80, WorldW - 80);
            y = rng.Next(80, WorldH - 80);
        }
        while (MathF.Abs(x - rocketX) < 200 && MathF.Abs(y - rocketY) < 200);
        var rotation = rng.Next(0, 360);
        var heading = rng.Next(0, 360);
        var scale = rng.Next(2, 10) / 10f;
        var speed = rng.Next(20, 100);

        var asteriod = new Asteroid
        {
            Image = "asteroid.png",
            Center = new Vector2(x, y),
            Scale = scale,
            Rotation = rotation,
            Heading = heading,
            Speed = speed,
            RotationSpeed = rng.Next(-30, 31),
        };

        asteroids.Add(asteriod);
    }
    return asteroids;
}

sealed class RocketMinimapLayer : MinimapLayer2D
{
    protected override MinimapMarker? GetMarker(IEntity entity) => entity switch
    {
        Rocket r => new MinimapMarker(
            Color.Red,
            Radius: 6f,
            Shape: MinimapShape.Triangle,
            // Use Rotation (not Heading) so the marker spins with the
            // rocket during stun, when the two diverge.
            Rotation: r.Rotation),
        Asteroid { Kind: AsteriodKind.Gold } g =>
            new MinimapMarker(new Color(255, 215, 0), Radius: 2f + g.Scale * 4f, Shape: MinimapShape.Circle),
        Asteroid { Kind: AsteriodKind.Radioactive } x =>
            new MinimapMarker(new Color(120, 255, 120), Radius: 2f + x.Scale * 4f, Shape: MinimapShape.Square, Rotation: 45f),
        Asteroid a =>
            new MinimapMarker(new Color(210, 180, 140), Radius: 1.5f + a.Scale * 3.5f, Shape: MinimapShape.Circle),
        _ => null,
    };
}

// HUD overlay: flashing speed readout plus the "GAME OVER" banner that
// LevelComplete2D triggers during the exit delay.
sealed class RocketHud : Entity, IDrawable2D
{
    private Rocket? _rocket;
    private LevelComplete2D? _levelComplete;

    public string? RocketName { get; init; }

    public string? LevelCompleteName { get; init; }

    public required Font ScoreFont { get; init; }

    public required Font BannerFont { get; init; }

    public Vector2 DesignSize { get; init; }

    public void Draw(Renderer2D rd)
    {
        if (!TryResolveRocket(out var rocket))
            return;

        using var _ = rd.PushState();
        rd.Camera = null; // detach camera so HUD is screen-locked

        // Speed readout under the score. Reading rocket.Speed here each
        // frame keeps the HUD live. Below the smash threshold the
        // readout flashes to nag the player.
        const float SmashSpeed = 500f;
        bool tooSlow = rocket.Speed < SmashSpeed;
        bool flashOn = !tooSlow
            || ((int)(Environment.TickCount / 250) & 1) == 0;
        if (flashOn)
        {
            var speedColor = tooSlow
                ? new Color(255, 140, 80)  // orange = too slow
                : new Color(120, 255, 120); // green = smashing speed
            ScoreFont.DrawText(rd, $"SPEED {rocket.Speed:0}", speedColor, 20f, 120f);
        }

        if (TryResolveLevelComplete(out var levelComplete) && levelComplete.GameOverAt is not null)
        {
            const string banner = "GAME OVER";
            var size = BannerFont.Measure(banner);
            float x = (DesignSize.X - size.X) / 2f;
            float y = (DesignSize.Y - size.Y) / 2f;
            BannerFont.DrawText(rd, banner, Color.White, x, y);
        }
    }

    private bool TryResolveRocket([NotNullWhen(true)] out Rocket? rocket)
    {
        if (_rocket is not null)
        {
            rocket = _rocket;
            return true;
        }

        if (Container is not { } container || !container.TryGetEntity<PlayField2D>(out var playfield))
        {
            rocket = null;
            return false;
        }

        var found = playfield.TryGetEntity(RocketName, out rocket);

        if (found)
            _rocket = rocket;

        return found;
    }

    private bool TryResolveLevelComplete([NotNullWhen(true)] out LevelComplete2D? levelComplete)
    {
        if (_levelComplete is not null)
        {
            levelComplete = _levelComplete;
            return true;
        }

        if (Container is not { } container)
        {
            levelComplete = null;
            return false;
        }

        var found = LevelCompleteName is null
            ? container.TryGetBehavior(out levelComplete)
            : container.TryGetCapability(LevelCompleteName, out levelComplete);

        if (found)
            _levelComplete = levelComplete;

        return found;
    }
}

// Debug overlay: outlines the rocket and asteroid HitShapes in world
// space (toggle with H) so collision registration is visible.
sealed class HitDebugLayer : Entity, IDrawable2D, IUpdatable
{
    private readonly HitShapeDebug2D _hitShapeDebug = new();
    private PlayField2D? _playField;
    private Rocket? _rocket;
    private bool _showHitShape;

    public string? PlayFieldName { get; init; }

    public string? RocketName { get; init; }

    public void Update(in EntityUpdateContext context)
    {
        if (context.Input?.WasJustPressed(Key.H) == true)
            _showHitShape = !_showHitShape;
    }

    public void Draw(Renderer2D rd)
    {
        if (!_showHitShape) return;
        if (!TryResolvePlayField(out var playField) || !TryResolveRocket(playField, out var rocket)) return;

        using var _ = rd.PushState();

        rd.DrawColor = new Color(0, 255, 120, 220);
        _hitShapeDebug.Draw(rd, rocket.HitShape);
        // Also outline the bounding circle in a dimmer color
        // so we can see when the cheap reject would skip.
        rd.DrawColor = new Color(0, 200, 255, 90);
        HitShapeDebug2D.DrawCircleOutline(rd, rocket.HitShape.BoundingCircle.Center, rocket.HitShape.BoundingCircle.Radius);

        // Same treatment for every asteroid currently on the field:
        // magenta hit shape outline + dim bounding circle.
        foreach (var sprite in playField.Entities)
        {
            if (sprite is not Asteroid asteroid) continue;
            rd.DrawColor = new Color(255, 80, 200, 220);
            _hitShapeDebug.Draw(rd, asteroid.HitShape);
            rd.DrawColor = new Color(255, 120, 220, 70);
            HitShapeDebug2D.DrawCircleOutline(rd, asteroid.HitShape.BoundingCircle.Center, asteroid.HitShape.BoundingCircle.Radius);
        }
    }

    private bool TryResolvePlayField([NotNullWhen(true)] out PlayField2D? playField)
    {
        if (_playField is not null)
        {
            playField = _playField;
            return true;
        }

        if (Container is not { } container)
        {
            playField = null;
            return false;
        }

        var found = container.TryGetEntity(PlayFieldName, out playField);
        if (found)
            _playField = playField;

        return found;
    }

    private bool TryResolveRocket(PlayField2D playField, [NotNullWhen(true)] out Rocket? rocket)
    {
        if (_rocket is not null)
        {
            rocket = _rocket;
            return true;
        }

        var found = playField.TryGetEntity(RocketName, out rocket);
        if (found)
            _rocket = rocket;

        return found;
    }
}

// Ends the run when every non-radioactive target is cleared (or V is
// pressed). The HUD reads GameOverAt to draw the "GAME OVER" banner.
sealed class LevelComplete2D : Behavior, IUpdatable, ICapability
{
    private PlayField2D? _playField;

    public string? Name { get; init; }

    public TimeSpan Delay { get; init; }

    public string? PlayFieldName { get; init; }

    public DateTime? GameOverAt { get; private set; }

    public void Update(in EntityUpdateContext context)
    {
        var runControl = context.RunControl;
        if (runControl?.RunState != RunState.Running)
            return;
        if (!TryResolvePlayField(out var playField))
            return;

        var remainingTargets = playField.Entities.Count(
            s => s is Asteroid a && a.Kind != AsteriodKind.Radioactive);
        if (remainingTargets == 0 || context.Input?.WasJustPressed(Key.V) == true)
        {
            _ = Audio.PlayAsync(Melodies.LevelUp, volume: .3f);
            GameOverAt = DateTime.UtcNow;
            runControl.RequestExitAfter(Delay);
        }
    }

    private bool TryResolvePlayField([NotNullWhen(true)] out PlayField2D? playField)
    {
        if (_playField is not null)
        {
            playField = _playField;
            return true;
        }

        if (Entity.Container is not { } container)
        {
            playField = null;
            return false;
        }

        var found = container.TryGetEntity(PlayFieldName, out playField);
        if (found)
            _playField = playField;

        return found;
    }
}

sealed class Rocket : Sprite2D, IUpdatable
{
    /// <summary>
    /// The flame drawn behind the rocket while thrusting. A separate slot
    /// from <see cref="Sprite2D.Image"/> so it can be composited with its own
    /// blend mode. Assign a path; call <see cref="ImageSource.GetComposedVisual"/> to draw.
    /// </summary>
    public ImageSource Flame { get; set; } = new();

    public TimeSpan Elapsed { get; private set; }

    public TimeSpan FlameUntil { get; private set; }
    public bool IsFlameVisible => Elapsed < FlameUntil;

    public TimeSpan ShieldUntil { get; private set; }
    public bool IsShieldVisible => Elapsed < ShieldUntil;

    private const int ShieldVisualSize = 256;
    private const float ShieldDrawDiameterScale = 2.35f;

    // Pre-painted shield image, created once and reused every frame.
    // The image is oriented nose-up (heading=0) and rotated in Draw().
    private static readonly Visual2D ShieldVisual = MakeShieldVisual(ShieldVisualSize);

    // Set by SmashAsteroidBehavior on a radioactive hit. While
    // Elapsed < StunUntil the rocket spins freely, ignores input, and
    // bounces off asteroids instead of smashing them.
    public TimeSpan StunUntil { get; set; }
    public bool IsStunned => Elapsed < StunUntil;

    public Rocket()
    {
        this.Behaviors =
        [
            new Motion2D(),  // move with simple 2D physics
            // Face direction of travel, except while stunned — then
            // let RotationSpeed drive the spin freely.
            new FaceHeadingUnlessStunned(),
            new BounceInBounds2D { Bounced = new BounceJitter() },  // bounce off the walls
        ];
    }

    public void Update(in EntityUpdateContext context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;
    }

    private sealed class FaceHeadingUnlessStunned : Behavior, IUpdatable
    {
        public void Update(in EntityUpdateContext context)
        {
            if (Entity is not Sprite2D s || s is Rocket { IsStunned: true })
                return;
            s.Rotation = s.Heading;
        }
    }

    // On a wall bounce, nudge the heading randomly and play a boing.
    private sealed class BounceJitter : IEventHandler<BoundsBounced2DEventArgs>
    {
        public void OnEvent(in BoundsBounced2DEventArgs e)
        {
            if (e.Self is not Sprite2D s)
                return;
            s.Heading = (s.Heading + Random.Shared.Next(-10, 10) + 360f) % 360f;
            Audio.Play(Sounds.Boing, volume: .2f);
        }
    }

    public void Stun(TimeSpan duration)
    {
        StunUntil = Elapsed + duration;
        // Random direction & rate for the spin.
        var rate = Random.Shared.Next(240, 540);
        if (Random.Shared.Next(2) == 0) rate = -rate;
        this.RotationSpeed = rate;
    }

    public void ShowFlameFor(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return;

        var until = Elapsed + duration;
        if (until > FlameUntil)
            FlameUntil = until;
    }

    public void TriggerShieldFlash(TimeSpan duration = default)
    {
        if (duration == default)
            duration = TimeSpan.FromMilliseconds(150);

        var until = Elapsed + duration;
        if (until > ShieldUntil)
            ShieldUntil = until;
    }

    public override void Draw(Renderer2D renderer)
    {
        var pose = new Pose2D(Center, Rotation, Scale);

        if (IsFlameVisible)
            Flame.GetComposedVisual()?.Draw(renderer, pose, Color.White, Elapsed, Flipped);

        Image.GetComposedVisual()?.Draw(renderer, pose, Tint, Elapsed, Flipped);

        // Shield image drawn at the bounding circle size, rotated with the heading.
        if (IsShieldVisible)
        {
            const float ShieldDuration = 0.15f;
            var shieldAge = (float)(Elapsed - (ShieldUntil - TimeSpan.FromSeconds(ShieldDuration))).TotalSeconds;
            var alpha = Math.Clamp(1f - shieldAge / ShieldDuration, 0f, 1f);
            var shieldRadius = HitShape.BoundingCircle.Radius;
            var shieldScale = (shieldRadius * ShieldDrawDiameterScale) / ShieldVisualSize;
            var shieldPose = new Pose2D(Center, Heading, shieldScale);
            var tint = new Color(255, 255, 255, (byte)(255 * alpha));
            ShieldVisual.Draw(renderer, shieldPose, tint, Elapsed, Flipped);
        }
    }

    private static Visual2D MakeShieldVisual(int size)
    {
        var bitmap = Bitmap.Create(size, size);
        bitmap.DrawCanvas(canvas =>
        {
            canvas.Clear(SKColors.Transparent);
            float cx = size / 2f;
            float cy = size / 2f;
            float r = size / 2f - 2f;
            var circle = new SKRect(cx - r, cy - r, cx + r, cy + r);

            canvas.SaveLayer();

            // Outer halo sits a bit forward so the front rim reads brighter than the back.
            using (var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(
                    center: new SKPoint(cx, cy - r * 0.18f),
                    radius: r * 1.05f,
                    colors:
                    [
                        new SKColor(80, 180, 255, 0),
                        new SKColor(100, 210, 255, 35),
                        new SKColor(90, 200, 255, 135),
                        new SKColor(55, 145, 235, 190),
                        new SKColor(25, 90, 210, 0),
                    ],
                    colorPos: [0f, 0.52f, 0.70f, 0.84f, 1f],
                    mode: SKShaderTileMode.Clamp),
            })
            {
                canvas.DrawOval(circle, paint);
            }

            // Core glow is concentrated on the front half so the shield reads as a lit hemisphere.
            using (var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(
                    center: new SKPoint(cx, cy - r * 0.42f),
                    radius: r * 0.95f,
                    colors:
                    [
                        new SKColor(220, 245, 255, 210),
                        new SKColor(120, 210, 255, 150),
                        new SKColor(70, 165, 235, 60),
                        new SKColor(20, 90, 210, 0),
                    ],
                    colorPos: [0f, 0.26f, 0.58f, 1f],
                    mode: SKShaderTileMode.Clamp),
            })
            {
                canvas.DrawOval(circle, paint);
            }

            // A thin forward rim helps the shield read larger without filling in the back edge.
            using (var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = r * 0.10f,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(cx, cy - r),
                    new SKPoint(cx, cy + r),
                    [
                        new SKColor(200, 235, 255, 210),
                        new SKColor(100, 190, 255, 110),
                        new SKColor(40, 120, 220, 0),
                    ],
                    [0f, 0.45f, 1f],
                    SKShaderTileMode.Clamp),
            })
            {
                canvas.DrawOval(circle, paint);
            }

            // Small specular highlight dot near the nose.
            using (var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(
                    center: new SKPoint(cx, cy - r * 0.58f),
                    radius: r * 0.18f,
                    colors:
                    [
                        new SKColor(255, 255, 255, 235),
                        new SKColor(255, 255, 255, 0),
                    ],
                    colorPos: [0f, 1f],
                    mode: SKShaderTileMode.Clamp),
            })
            {
                canvas.DrawCircle(cx, cy - r * 0.58f, r * 0.18f, paint);
            }

            // Alpha mask keeps the front strong and lets the shield die off around the back,
            // so it reads like a partial lit sphere instead of a complete glowing ball.
            using (var paint = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn,
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(cx, cy - r),
                    new SKPoint(cx, cy + r),
                    [
                        new SKColor(255, 255, 255, 255),
                        new SKColor(255, 255, 255, 230),
                        new SKColor(255, 255, 255, 110),
                        new SKColor(255, 255, 255, 20),
                        new SKColor(255, 255, 255, 0),
                    ],
                    [0f, 0.38f, 0.62f, 0.82f, 1f],
                    SKShaderTileMode.Clamp),
            })
            {
                canvas.DrawOval(circle, paint);
            }

            canvas.Restore();
        });
        return new TextureVisual2D(bitmap);
    }
}

sealed class AsteroidSmasher : Behavior, IHittable2D
{
    private readonly ScoreLayer2D _scoreboard;

    // Combo tracking: each consecutive scoring smash within
    // ComboWindow multiplies the points awarded. Resets when the
    // window expires.
    private TimeSpan _comboExpiresAt = TimeSpan.Zero;
    private int _comboCount = 0;
    private static readonly TimeSpan ComboWindow = TimeSpan.FromSeconds(2.0);
    private const int ComboMaxMultiplier = 8;
    private static readonly Color _comboTint = new Color(255, 240, 120);

    // Slower, more menacing klaxon than the stock Sounds.Klaxon —
    // 0.32 s per beat instead of 0.15 s. Cached so we don't
    // resynthesize the buffer on every collision.
    private static readonly Sound _radioactiveAlarm =
        Sounds.CreateKlaxon(duration: 1.6f, beatDuration: 0.32f);

    public AsteroidSmasher(ScoreLayer2D scoreboard)
    {
        _scoreboard = scoreboard;
    }

    public void OnHit(in Hit2D hit)
    {
        if (this.Entity is not Rocket rocket || hit.Other is not Asteroid asteroid)
            return;

        // Grace period: ignore freshly-spawned shards so they
        // can spread out before being hit again. Without this
        // the rocket sits inside the impact zone and devours
        // every shard on the very next frame.
        if (asteroid.Elapsed < TimeSpan.FromMilliseconds(400))
            return;

        // Shield flash on any collision (before deflect/stun/smash logic)
        rocket.TriggerShieldFlash();

        // Cooldown after a deflect so we don't re-trigger every
        // frame while the rocket is still overlapping this rock.
        if (asteroid.Elapsed < asteroid.HitCooldownUntil)
            return;

        // Capture impact speed before applying momentum loss so the
        // smash-threshold check reflects how fast the player was
        // actually going at the moment of contact.
        float impactSpeed = rocket.Speed;

        // Any hit costs momentum — scaled by the asteroid's mass so
        // the big rocks really pull you up short. Player has to lean
        // on the thrusters to recover.
        var loss = 50f * asteroid.Scale;
        rocket.Speed = Math.Max(0f, rocket.Speed - loss);

        // Stunned rocket can't smash — it caroms off instead, and
        // shoves the asteroid the other way. No score, no shards.
        if (rocket.IsStunned)
        {
            var delta = rocket.Center - asteroid.Center;
            var awayFromAsteroid = MathF.Atan2(delta.Y, delta.X) * 180f / MathF.PI + 90f;
            rocket.Heading = (awayFromAsteroid + 360f) % 360f;
            asteroid.Heading = (awayFromAsteroid + 180f) % 360f;
            asteroid.HitCooldownUntil = asteroid.Elapsed + TimeSpan.FromMilliseconds(400);
            Audio.Play(Sounds.Boing, volume: .2f);
            return;
        }

        // Not going fast enough to smash through — deflect off
        // instead. Nudge each heading partway toward the
        // away-from-impact direction (shortest arc) so the rocket
        // visibly veers without flipping all the way around.
        const float SmashSpeed = 500f;
        if (impactSpeed < SmashSpeed)
        {
            var delta = rocket.Center - asteroid.Center;
            var awayFromAsteroid = MathF.Atan2(delta.Y, delta.X) * 180f / MathF.PI + 90f;
            rocket.Heading = NudgeToward(rocket.Heading, awayFromAsteroid, fraction: 0.35f, jitterDeg: 5f, maxDeg: 20f);
            asteroid.Heading = NudgeToward(asteroid.Heading, awayFromAsteroid + 180f, fraction: 0.5f, jitterDeg: 8f, maxDeg: 25f);
            asteroid.HitCooldownUntil = asteroid.Elapsed + TimeSpan.FromMilliseconds(400);
            Audio.Play(Sounds.Hurt, volume: .1f);
            return;
        }

        // Mark the meteor for removal; the playfield reaps it
        // on its next update pass.
        asteroid.PlayField.RemoveEntity(asteroid);
        rocket.Heading = (rocket.Heading + Random.Shared.Next(-15, 15) + 360f) % 360f;
        Audio.Play(Sounds.Explosion, volume: .3f);

        // Score & popup for gold ("valuable minerals") asteroids.
        if (asteroid.Kind == AsteriodKind.Gold)
        {
            // Bigger rocks are worth more; round to tens.
            int points = Math.Max(10, (int)Math.Round(asteroid.Scale * 200f / 10f) * 10);

            // Combo: chain consecutive gold smashes within the
            // window for an N× multiplier (capped). Each scoring
            // hit extends the window from itself.
            if (rocket.Elapsed < _comboExpiresAt)
                _comboCount++;
            else
                _comboCount = 1;
            _comboExpiresAt = rocket.Elapsed + ComboWindow;
            int multiplier = Math.Min(_comboCount, ComboMaxMultiplier);

            _scoreboard.Add(points * multiplier, asteroid.Center);
            Audio.Play(Sounds.Coin, volume: .1f);

            // Telegraph the multiplier so the player knows the
            // bonus is in effect (and is worth chasing).
            if (multiplier > 1 && _scoreboard.Popups is { } pops)
            {
                pops.Add(
                    $"x{multiplier} COMBO!",
                    asteroid.Center + new Vector2(0f, -60f),
                    _comboTint,
                    scale: 1f + 0.08f * (multiplier - 1));
            }
        }

        // Radioactive: stun the rocket. It keeps drifting at its
        // current speed but spins freely, ignores the player's
        // controls, and bounces off rather than smashing asteroids
        // until the stun wears off.
        if (asteroid.Kind == AsteriodKind.Radioactive)
        {
            // Radioactive hit breaks any combo streak — no chaining
            // through a penalty.
            _comboCount = 0;
            _comboExpiresAt = TimeSpan.Zero;

            var stunDuration = TimeSpan.FromSeconds(4 * asteroid.Scale);
            rocket.Stun(stunDuration);
            Audio.Play(_radioactiveAlarm, volume: .2f);

            // Bigger rocks hurt more; round to tens — same shape as
            // the gold reward formula.
            int penalty = Math.Max(10, (int)Math.Round(asteroid.Scale * 200f / 10f) * 10);
            // Clamp at zero — negative scores aren't fun.
            var deducted = (int)Math.Min(_scoreboard.Score, penalty);
            if (deducted > 0)
                _scoreboard.Add(-deducted, asteroid.Center);
        }

        // split the asteroid into shards
        asteroid.Smash();
        asteroid.PlayField.RemoveEntity(asteroid);
    }

    private static float NudgeToward(float current, float target, float fraction, float jitterDeg, float maxDeg)
    {
        // Shortest-arc delta in [-180, 180], move part of the way,
        // then clamp the signed step so a head-on impact still only
        // produces a gentle deflection.
        var delta = ((target - current + 540f) % 360f) - 180f;
        var step = Math.Clamp(delta * fraction, -maxDeg, maxDeg);
        var jitter = (float)(Random.Shared.NextDouble() * 2 - 1) * jitterDeg;
        return (current + step + jitter + 360f) % 360f;
    }
}

enum AsteriodKind
{
    Regular,
    Gold,
    Radioactive
}

sealed class Asteroid : Sprite2D, IUpdatable
{
    public static readonly float GoldRarity = 0.25f; // 25% of asteroid shards are gold
    public static readonly float RadioactiveRarity = 0.1f; // 10% of asteroid shards are radioactive

    public AsteriodKind Kind { get; }

    public TimeSpan Elapsed { get; private set; }

    // While Elapsed < HitCooldownUntil this asteroid ignores rocket
    // contacts — used after a deflect so we don't keep colliding
    // every frame while the two are still overlapping.
    public TimeSpan HitCooldownUntil { get; set; }

    // Optional debris pool emitted when the smallest asteroids are
    // destroyed. Set once at startup; null disables the effect.
    public static ParticleLayer2D? Particles { get; set; }

    private static readonly Color _goldTint = new Color(255, 215, 0);
    private static readonly Color _radioactiveTint = new Color(90, 200, 90);

    public Asteroid(AsteriodKind kind = AsteriodKind.Regular)
    {
        this.Kind = kind;
        if (kind == AsteriodKind.Gold)
        {
            this.Tint = _goldTint;
        }
        else if (kind == AsteriodKind.Radioactive)
        {
            this.Tint = _radioactiveTint;
            this.GetOrAddBehavior<PulseTint2D>().SetBrightness(_radioactiveTint, amount: 0.5f, period: TimeSpan.FromSeconds(0.6));
        }

        this.GetOrAddBehavior<Motion2D>();
        this.GetOrAddBehavior<BounceInBounds2D>();
    }

    public void Update(in EntityUpdateContext context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;
    }

    public void Smash()
    {
        // Smallest asteroids vanish in a colored puff instead of
        // spawning further shards.
        if (this.Scale <= 0.25f)
        {
            EmitDebris();
            return;
        }

        // only break larger asteroids
        {
            var playfield = this.PlayField;

            var newScale = this.Scale * 0.5f;
            var shardCount = Random.Shared.Next(2, 4);
            for (int i = 0; i < shardCount; i++)
            {
                var shardSpin = Random.Shared.Next(120, 360);
                if (Random.Shared.Next(2) == 0)
                    shardSpin = -shardSpin;
                var rnd = Random.Shared.NextDouble();
                var childKind = 
                    (this.Kind == AsteriodKind.Gold || rnd < Asteroid.GoldRarity)
                        ? AsteriodKind.Gold 
                    : (this.Kind == AsteriodKind.Radioactive || rnd < Asteroid.GoldRarity + Asteroid.RadioactiveRarity)
                        ? AsteriodKind.Radioactive 
                    : AsteriodKind.Regular;

                var shard = new Asteroid(childKind)
                {
                    Image = this.Image,
                    Center = this.Center,
                    Scale = newScale,
                    Rotation = Random.Shared.Next(0, 360),
                    Heading = Random.Shared.Next(0, 360),
                    Speed = Random.Shared.Next(80, 180),
                    RotationSpeed = shardSpin,
                };  

                playfield.AddEntity(shard);
            }           
        }
    }

    private void EmitDebris()
    {
        var particles = Particles;
        if (particles is null) return;
        var tint = this.Kind switch
        {
            AsteriodKind.Gold => _goldTint,
            AsteriodKind.Radioactive => _radioactiveTint,
            _ => new Color(210, 180, 140),
        };
        var style = new ParticleStyle
        {
            LifetimeRange = new Vector2(0.4f, 0.9f),
            SpeedRange = new Vector2(120f, 280f),
            StartTint = tint,
            EndTint = new Color(tint.R, tint.G, tint.B, 0),
        };
        particles.Emit(this.Center, count: 28, style);
    }
}

sealed class RocketController : Behavior, IUpdatable
{
    // Turn feel tuning.
    private const float MaxTurnRateDegPerSec = 240f;
    private const float TurnAccelDegPerSec2 = 1200f;
    private const float TurnDecelDegPerSec2 = 1800f;
    private const float TapTurnKickDegPerSec = 85f;

    // Signed turn rate in deg/s. Negative = left, positive = right.
    private float _turnRateDegPerSec;

    private Sprite2D _rocket = null!;

    protected override void OnAttach(IEntity entity) => _rocket = (Sprite2D)entity;

    public void Update(in EntityUpdateContext context)
    {
        var rocket = _rocket;

        // No control input during stun.
        if (rocket is Rocket { IsStunned: true })
        {
            _turnRateDegPerSec = 0f;
            return;
        }

        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var input = context.Input;
        if (input is null)
            return;

        var leftDown = input.IsDown(Key.Left);
        var rightDown = input.IsDown(Key.Right);

        float targetTurnRate = 0f;
        if (leftDown && !rightDown)
            targetTurnRate = -MaxTurnRateDegPerSec;
        else if (rightDown && !leftDown)
            targetTurnRate = MaxTurnRateDegPerSec;

        // Snap toward target while held; otherwise decay back to zero.
        var maxStep = (targetTurnRate == 0f ? TurnDecelDegPerSec2 : TurnAccelDegPerSec2) * dt;
        _turnRateDegPerSec = MoveToward(_turnRateDegPerSec, targetTurnRate, maxStep);

        // Tap response: a short press injects an immediate turn kick.
        if (input.WasJustPressed(Key.Left))
            _turnRateDegPerSec -= TapTurnKickDegPerSec;
        if (input.WasJustPressed(Key.Right))
            _turnRateDegPerSec += TapTurnKickDegPerSec;

        _turnRateDegPerSec = Math.Clamp(_turnRateDegPerSec, -MaxTurnRateDegPerSec, MaxTurnRateDegPerSec);
        rocket.Heading = WrapDegrees(rocket.Heading + _turnRateDegPerSec * dt);

        if (input.WasJustPressed(Key.Up))
        {
            rocket.Speed = Math.Clamp(rocket.Speed + 50f, 0f, 1000f);
            if (rocket is Rocket r)
                r.ShowFlameFor(Sounds.RoarUp.Duration);
            Audio.Play(Sounds.RoarUp, volume: .25f);
        }
        if (input.WasJustPressed(Key.Down))
        {
            rocket.Speed = Math.Clamp(rocket.Speed - 50f, 0f, 1000f);
            Audio.Play(Sounds.RoarDown, volume: .25f);
        }
    }

    private static float MoveToward(float current, float target, float maxStep)
    {
        if (current < target)
            return Math.Min(current + maxStep, target);
        return Math.Max(current - maxStep, target);
    }

    private static float WrapDegrees(float deg)
    {
        deg %= 360f;
        return deg < 0f ? deg + 360f : deg;
    }
}

