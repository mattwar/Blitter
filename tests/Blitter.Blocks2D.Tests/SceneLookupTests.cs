using System.Numerics;

namespace Blitter.Blocks2D.Tests;

public class SceneLookupTests
{
    // Wires entities into a root container without needing a window/renderer.
    private static Container SceneWith(params IEntity[] entities)
    {
        var scene = new Container();
        foreach (var entity in entities)
        {
            scene.AddEntity(entity);
        }
        return scene;
    }

    [Fact]
    public void AttachedCamera2D_Draw_AssignsRendererCamera()
    {
        var camera = new Camera2D();
        var scene = new Container
        {
            Behaviors = [new AttachedCamera2D { Camera = camera }]
        };
        var window = new Window2D { LogicalSize = (64, 64) };

        Drawer2D.Default.Draw(scene, window.Renderer);

        Assert.Same(camera, window.Renderer.Camera);
    }

    [Fact]
    public void AttachedCamera2D_DisabledDraw_DoesNotAssignRendererCamera()
    {
        var camera = new Camera2D();
        var scene = new Container
        {
            Behaviors = [new AttachedCamera2D { Camera = camera, Enabled = false }]
        };
        var window = new Window2D { LogicalSize = (64, 64) };

        Drawer2D.Default.Draw(scene, window.Renderer);

        Assert.Null(window.Renderer.Camera);
    }

    [Fact]
    public void TryGetEntityWithCapability_ByName_UsesCapabilityPicker()
    {
        var cameraEntity = new Entity { Behaviors = [new AttachedCamera2D { Name = "world" }] };
        var scene = SceneWith(cameraEntity);

        Assert.True(scene.TryGetEntityWithCapability<ICamera2D>("world", out var entity));
        Assert.Same(cameraEntity, entity);
    }

    [Fact]
    public void TryGetEntityWithCapability_ByName_MissingReturnsFalse()
    {
        var scene = SceneWith(new Entity { Behaviors = [new AttachedCamera2D { Name = "main" }] });
        Assert.False(scene.TryGetEntityWithCapability<ICamera2D>("nope", out var entity));
        Assert.Null(entity);
    }

    [Fact]
    public void CameraFollow2D_ResolvesCameraByType()
    {
        var attachedCamera = new AttachedCamera2D();
        var sprite = new Sprite2D();
        sprite.Center = new Vector2(20f, 0f);
        var playfield = new PlayField2D();
        var follow = sprite.GetOrAddBehavior<CameraFollow2D>();
        follow.ViewportSize = new Vector2(100f, 100f);
        follow.MarginFraction = 0.5f;
        playfield.AddEntity(sprite);
        new Container { Behaviors = [attachedCamera], Entities = [playfield] };

        Updater.Default.Update(sprite, new EntityUpdateContext());

        Assert.Equal(new Vector2(20f, 0f), attachedCamera.Camera.Position);
    }

    [Fact]
    public void CameraFollow2D_ResolvesCameraByWalkingUpTree()
    {
        var attachedCamera = new AttachedCamera2D();
        var target = new Entity();
        target.GetOrAddTrait<Transform2D>().Position = new Vector2(20f, 0f);
        var group = new Container { Entities = [target] };
        _ = new Container { Behaviors = [attachedCamera], Entities = [group] };
        var follow = target.GetOrAddBehavior<CameraFollow2D>();
        follow.ViewportSize = new Vector2(100f, 100f);
        follow.MarginFraction = 0.5f;

        Updater.Default.Update(target, new EntityUpdateContext());

        Assert.Equal(new Vector2(20f, 0f), attachedCamera.Camera.Position);
    }

    [Fact]
    public void CameraFollow2D_ResolvesCameraByName()
    {
        var world = new AttachedCamera2D { Name = "world" };
        var hud = new AttachedCamera2D { Name = "hud" };
        var sprite = new Sprite2D();
        sprite.Center = new Vector2(20f, 0f);
        var playfield = new PlayField2D();
        var follow = sprite.GetOrAddBehavior<CameraFollow2D>();
        follow.CameraName = "world";
        follow.ViewportSize = new Vector2(100f, 100f);
        follow.MarginFraction = 0.5f;
        playfield.AddEntity(sprite);
        _ = new Container { Behaviors = [world, hud], Entities = [playfield] };

        Updater.Default.Update(sprite, new EntityUpdateContext());

        Assert.Equal(new Vector2(20f, 0f), world.Camera.Position);
        Assert.Equal(Vector2.Zero, hud.Camera.Position);
    }

    [Fact]
    public void CameraFollow2D_DoesNotResolveCameraFromSiblingEntity()
    {
        var sibling = new Entity { Behaviors = [new AttachedCamera2D()] };
        var target = new Entity();
        target.GetOrAddTrait<Transform2D>().Position = new Vector2(20f, 0f);
        var follow = target.GetOrAddBehavior<CameraFollow2D>();
        follow.ViewportSize = new Vector2(100f, 100f);
        follow.MarginFraction = 0.5f;
        _ = new Container { Entities = [sibling, target] };

        Updater.Default.Update(target, new EntityUpdateContext());

        Assert.Equal(Vector2.Zero, sibling.GetCapability<ICamera2D>().Camera.Position);
    }
}
