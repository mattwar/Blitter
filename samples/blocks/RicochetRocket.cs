#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run RicochetRocket.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

using System.Numerics;
using Blitter;
using Blitter.Bits;
using Blitter.Blocks;

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
};

window.Renderer.SetLogicalSize(DesignW, DesignH, LogicalPresentation.Letterbox);

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

// Set by the exit behavior when the run ends. The HUD layer reads
// this to draw a "GAME OVER" banner during the exit-delay window.
DateTime? gameOverAt = null;
var gameOverDuration = TimeSpan.FromSeconds(3);

// create rocket sprite
var rocketImage = Bitmap.Load(Asset.GetPathRelativeToCaller("rocket.png"));
 // make rocket's background transparent
rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0));

// create meteor field - small static obstacles for the rocket to hit
var asteroidImage = Bitmap.Load(Asset.GetPathRelativeToCaller("asteroid.png"));
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
    Image = rocketImage,
    Center = new Vector2(WorldW / 2f, WorldH / 2f),
    Scale = 0.1f,
    Speed = 600f,
    Heading = 45f,
    Behaviors = 
    { 
        new RocketController(window.Input),
        new AsteroidSmasher(scoreboard),
    }
};

// Camera scrolls the world to keep the rocket in view. Start it on
// the rocket so the first frame isn't a snap from the world origin.
var camera = new Camera2D { Position = rocket.Center };
window.Renderer.Camera = camera;

var worldBounds = new Rect(0, 0, WorldW, WorldH);
rocket.Behaviors.Add(new CameraFollow2D
{
    Camera = camera,
    ViewportSize = new Vector2(DesignW, DesignH),
    MarginFraction = 0.3f,
    WorldBounds = worldBounds,
});

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
    ParallaxFactor = Vector2.Zero,
    StarColor = new Color(180, 180, 220, 255),
};

var starsMid = new StarField2D(250, midBounds, seed: 2)
{
    ParallaxFactor = new Vector2(MidParallax, MidParallax),
    StarColor = new Color(220, 220, 255, 255),
};

// The playfield includes the asteriods and the rocket.
var playField = new PlayField2D([..asteroids, rocket])
{
    WorldBounds = worldBounds,
    ShowWorldBounds = true,
};

// HUD layer for overlay text
var hud = new CustomLayer2D
{
    OnRender = rd =>
    {
        using var _ = rd.PushState();
        rd.Camera = null; // detach camera so HUD is screen-locked

        // Speed readout under the score. The delegate runs every
        // frame, so just reading rocket.Speed here keeps the HUD
        // live — no separate update step needed. Below the smash
        // threshold the readout flashes to nag the player.
        const float SmashSpeed = 500f;
        bool tooSlow = rocket.Speed < SmashSpeed;
        bool flashOn = !tooSlow
            || ((int)(Environment.TickCount / 250) & 1) == 0;
        if (flashOn)
        {
            var speedColor = tooSlow
                ? new Color(255, 140, 80)  // orange = too slow
                : new Color(120, 255, 120); // green = smashing speed
            scoreFont.DrawText(rd, $"SPEED {rocket.Speed:0}", speedColor, 20f, 120f);
        }

#if false
        rd.DrawColor = Color.White;
        var asteriodCount = playField.Sprites.Count(s => s is Asteroid);
        rd.DrawDebugText(
            0, 10,
            $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} rotation: {rocket.Rotation:#} x: {rocket.Center.X:#} y: {rocket.Center.Y:#} asteriods: {asteriodCount}",
            scale: 2f);
#endif

        if (gameOverAt is not null)
        {
            const string banner = "GAME OVER";
            var size = bannerFont.Measure(banner);
            float x = (DesignW - size.X) / 2f;
            float y = (DesignH - size.Y) / 2f;
            bannerFont.DrawText(rd, banner, Color.White, x, y);
        }
    }
};

// Overhead minimap so the player can see asteroids beyond the
// viewport. Sized to the upper-right corner; markers are scaled
// by asteroid size and colored by kind.
var minimap = new MinimapLayer2D
{
    Source = playField,
    // 16:9 to match the world (3840x2160) so the viewport overlay
    // and asteroid positions aren't stretched.
    ScreenRect = new Rect(DesignW - 340, 20, 320, 180),
    // Slightly translucent so asteroids passing behind the minimap
    // are still readable through the panel.
    BackgroundColor = new Color(0, 0, 0, 100),
    ViewportCamera = camera,
    ViewportSize = new Vector2(DesignW, DesignH),
    ViewportColor = new Color(0,0,0,0), // no outline
    MarkerSelector = s => s switch
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
    },
};

// Debug overlay: outlines the rocket's HitShape in world space so
// we can see exactly where collisions register. Toggle with H.
bool showHitShape = false;
var hitDebug = new CustomLayer2D
{
    OnUpdate = ctx =>
    {
        if (window.Input.WasJustPressed(Key.H))
            showHitShape = !showHitShape;
    },
    OnRender = rd =>
    {
        if (!showHitShape) return;
        using var _ = rd.PushState();
        rd.DrawColor = new Color(0, 255, 120, 220);
        rocket.HitShape.Visit(HitShapeDebug.Draw(rd));
        // Also outline the broad-phase circle in a dimmer color
        // so we can see when the cheap reject would skip.
        rd.DrawColor = new Color(0, 200, 255, 90);
        HitShapeDebug.DrawCircleOutline(rd, rocket.HitShape.BroadCircle.Center, rocket.HitShape.BroadCircle.Radius);

        // Same treatment for every asteroid currently on the field:
        // magenta hit shape outline + dim broad circle.
        var hitVisitor = HitShapeDebug.Draw(rd);
        foreach (var sprite in playField.Sprites)
        {
            if (sprite is not Asteroid asteroid) continue;
            rd.DrawColor = new Color(255, 80, 200, 220);
            asteroid.HitShape.Visit(hitVisitor);
            rd.DrawColor = new Color(255, 120, 220, 70);
            HitShapeDebug.DrawCircleOutline(rd, asteroid.HitShape.BroadCircle.Center, asteroid.HitShape.BroadCircle.Radius);
        }
    },
};

var scene = new Scene2D
{
    Layers = 
    { 
        starsFar, 
        starsMid, 
        playField,
        debrisParticles,
        hitDebug,
        popups,
        scoreboard,
        hud,
        minimap,
    },
    Behaviors =
    {
        new CustomSceneBehavior2D()
        {
            OnApply = (s, in ctx) =>
            {
                if (s.RunState != RunState.Running)
                    return;
                var remainingTargets = playField.Sprites.Count(s => s is Asteroid a && a.Kind != AsteriodKind.Radioactive);
                if (remainingTargets == 0 || window.Input.WasJustPressed(Key.V))
                {
                    var task = Audio.PlayAsync(Melodies.LevelUp, volume: .3f);
                    gameOverAt = DateTime.UtcNow;
                    s.ExitWithDelay(gameOverDuration);
                }
            }
        }
    },
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
            Image = image,
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

sealed class Rocket : Sprite2D
{
    // Set by SmashAsteroidBehavior on a radioactive hit. While
    // Age < StunUntil the rocket spins freely, ignores input, and
    // bounces off asteroids instead of smashing them.
    public TimeSpan StunUntil { get; set; }
    public bool IsStunned => Age < StunUntil;

    public Rocket()
    {
        this.Behaviors.AddRange([
            new Motion2D(),  // move with simple 2D physics
            // Face direction of travel, except while stunned — then
            // let RotationSpeed drive the spin freely.
            new CustomSpriteBehavior2D
            {
                OnApply = (s, in _) =>
                {
                    if (s is Rocket { IsStunned: true })
                        return;
                    s.Rotation = s.Heading;
                },
            },
            new BounceInBounds2D  // bounce off the walls
            {
                OnBounce = s =>
                {
                    s.Heading = (s.Heading + Random.Shared.Next(-10, 10) + 360f) % 360f;
                    Audio.Play(Sounds.Boing, volume: .2f);
                },
            },
        ]);
    }

    public void Stun(TimeSpan duration)
    {
        StunUntil = Age + duration;
        // Random direction & rate for the spin.
        var rate = Random.Shared.Next(240, 540);
        if (Random.Shared.Next(2) == 0) rate = -rate;
        this.RotationSpeed = rate;
    }
}

sealed class AsteroidSmasher : SpriteBehavior2D
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

    public override void OnHitSprite(Sprite2D self, Sprite2D other, in UpdateContext2D context)
    {
        // Grace period: ignore freshly-spawned shards so they
        // can spread out before being hit again. Without this
        // the rocket sits inside the impact zone and devours
        // every shard on the very next frame.
        if (other.Age < TimeSpan.FromMilliseconds(400))
            return;

        if (other is not Asteroid asteroid)
            return;

        // Cooldown after a deflect so we don't re-trigger every
        // frame while the rocket is still overlapping this rock.
        if (asteroid.Age < asteroid.HitCooldownUntil)
            return;

        // Capture impact speed before applying momentum loss so the
        // smash-threshold check reflects how fast the player was
        // actually going at the moment of contact.
        float impactSpeed = self is Rocket r0 ? r0.Speed : 0f;

        // Any hit costs momentum — scaled by the asteroid's mass so
        // the big rocks really pull you up short. Player has to lean
        // on the thrusters to recover.
        if (self is Rocket hitRocket)
        {
            var loss = 50f * asteroid.Scale;
            hitRocket.Speed = Math.Max(0f, hitRocket.Speed - loss);
        }

        // Stunned rocket can't smash — it caroms off instead, and
        // shoves the asteroid the other way. No score, no shards.
        if (self is Rocket { IsStunned: true })
        {
            var delta = self.Center - asteroid.Center;
            var awayFromAsteroid = MathF.Atan2(delta.Y, delta.X) * 180f / MathF.PI + 90f;
            self.Heading = (awayFromAsteroid + 360f) % 360f;
            asteroid.Heading = (awayFromAsteroid + 180f) % 360f;
            asteroid.HitCooldownUntil = asteroid.Age + TimeSpan.FromMilliseconds(400);
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
            var delta = self.Center - asteroid.Center;
            var awayFromAsteroid = MathF.Atan2(delta.Y, delta.X) * 180f / MathF.PI + 90f;
            self.Heading = NudgeToward(self.Heading, awayFromAsteroid, fraction: 0.35f, jitterDeg: 5f, maxDeg: 20f);
            asteroid.Heading = NudgeToward(asteroid.Heading, awayFromAsteroid + 180f, fraction: 0.5f, jitterDeg: 8f, maxDeg: 25f);
            asteroid.HitCooldownUntil = asteroid.Age + TimeSpan.FromMilliseconds(400);
            Audio.Play(Sounds.Hurt, volume: .1f);
            return;
        }

        // Mark the meteor for removal; the playfield reaps it
        // on its next update pass.
        other.IsAlive = false;
        self.Heading = (self.Heading + Random.Shared.Next(-15, 15) + 360f) % 360f;
        Audio.Play(Sounds.Explosion, volume: .3f);

        // Score & popup for gold ("valuable minerals") asteroids.
        if (asteroid.Kind == AsteriodKind.Gold)
        {
            // Bigger rocks are worth more; round to tens.
            int points = Math.Max(10, (int)Math.Round(asteroid.Scale * 200f / 10f) * 10);

            // Combo: chain consecutive gold smashes within the
            // window for an N× multiplier (capped). Each scoring
            // hit extends the window from itself.
            if (self.Age < _comboExpiresAt)
                _comboCount++;
            else
                _comboCount = 1;
            _comboExpiresAt = self.Age + ComboWindow;
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
        if (asteroid.Kind == AsteriodKind.Radioactive
            && self is Rocket r)
        {
            // Radioactive hit breaks any combo streak — no chaining
            // through a penalty.
            _comboCount = 0;
            _comboExpiresAt = TimeSpan.Zero;

            var stunDuration = TimeSpan.FromSeconds(4 * asteroid.Scale);
            r.Stun(stunDuration);
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
        asteroid.IsAlive = false;
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

sealed class Asteroid : Sprite2D
{
    public static readonly float GoldRarity = 0.25f; // 25% of asteroid shards are gold
    public static readonly float RadioactiveRarity = 0.1f; // 10% of asteroid shards are radioactive

    public AsteriodKind Kind { get; }

    // While Age < HitCooldownUntil this asteroid ignores rocket
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
            this.Behaviors.Add(PulseTint2D.FromBrightness(_radioactiveTint, amount: 0.5f, period: TimeSpan.FromSeconds(0.6)));
        }

        this.Behaviors.Add(new Motion2D());
        this.Behaviors.Add(new BounceInBounds2D());
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

                playfield.AddSprite(shard);
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

sealed class RocketController : SpriteBehavior2D
{
    private readonly FrameInput _input;

    public RocketController(FrameInput input) => _input = input;

    public override void Apply(Sprite2D rocket, in UpdateContext2D context)
    {
        // No control input during stun.
        if (rocket is Rocket { IsStunned: true })
            return;

        if (_input.IsDown(Key.Left))
            rocket.Heading = (rocket.Heading + 350f) % 360f;

        if (_input.IsDown(Key.Right))
            rocket.Heading = (rocket.Heading + 10f) % 360f;

        if (_input.WasJustPressed(Key.Up))
        {
            rocket.Speed = Math.Clamp(rocket.Speed + 50f, 0f, 1000f);
            Audio.Play(Sounds.RoarUp, volume: .25f);
        }
        if (_input.WasJustPressed(Key.Down))
        {
            rocket.Speed = Math.Clamp(rocket.Speed - 50f, 0f, 1000f);
            Audio.Play(Sounds.RoarDown, volume: .25f);
        }
    }
}

/// <summary>
/// Tiny helper that turns a HitShape's live primitives into Renderer2D
/// line draws. Lives in the sample so the engine doesn't pull in a
/// rendering dependency for collision debug.
/// </summary>
static class HitShapeDebug
{
    // Build a visitor that draws each primitive. Returned delegate is
    // fresh each call but a debug overlay isn't on the hot path.
    public static HitShapeVisitor Draw(Renderer2D rd) => prims =>
    {
        for (int i = 0; i < prims.Length; i++)
        {
            var p = prims[i];
            switch (p.Kind)
            {
                case HitKind.Circle:
                    DrawCircleOutline(rd, p.P0, p.R);
                    break;
                case HitKind.Capsule:
                    DrawCapsuleOutline(rd, p.P0, p.P1, p.R);
                    break;
            }
        }
    };

    public static void DrawCircleOutline(Renderer2D rd, Vector2 center, float radius, int segments = 32)
    {
        Span<Vector2> pts = stackalloc Vector2[segments + 1];
        var step = MathF.Tau / segments;
        for (int i = 0; i <= segments; i++)
        {
            var a = i * step;
            pts[i] = new Vector2(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius);
        }
        rd.DrawLines(pts);
    }

    public static void DrawCapsuleOutline(Renderer2D rd, Vector2 a, Vector2 b, float radius, int capSegments = 12)
    {
        var axis = b - a;
        var len = axis.Length();
        // Degenerate capsule: just a circle.
        if (len <= float.Epsilon)
        {
            DrawCircleOutline(rd, a, radius, capSegments * 2);
            return;
        }
        var d = axis / len;
        var n = new Vector2(-d.Y, d.X) * radius;

        // Outline: side1 (A+n → B+n), B-cap arc, side2 (B-n → A-n),
        // A-cap arc, close. Drawn as one connected polyline.
        var totalPts = 1 + 1 + (capSegments + 1) + 1 + (capSegments + 1);
        Span<Vector2> pts = stackalloc Vector2[totalPts];
        int k = 0;
        pts[k++] = a + n;
        pts[k++] = b + n;
        // Cap at B: rotate +n around B by π in the +d half-plane.
        var startB = MathF.Atan2(n.Y, n.X);
        for (int i = 1; i <= capSegments + 1; i++)
        {
            var ang = startB - MathF.PI * i / capSegments;
            pts[k++] = b + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
        }
        // Now at b - n; walk back to a - n.
        pts[k++] = a - n;
        // Cap at A: continue the rotation another π so we land back at a + n.
        var startA = MathF.Atan2(-n.Y, -n.X);
        for (int i = 1; i <= capSegments + 1; i++)
        {
            var ang = startA - MathF.PI * i / capSegments;
            pts[k++] = a + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
        }
        rd.DrawLines(pts[..k]);
    }
}
