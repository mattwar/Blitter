namespace Blitter.Blocks2D.Tests;

public class SceneLookupTests
{
    // Wires layers into a root container without needing a window/renderer.
    private static ContainerEntity SceneWith(params Layer2D[] layers)
    {
        var scene = new ContainerEntity();
        foreach (var layer in layers)
        {
            scene.AddEntity(layer);
        }
        return scene;
    }

    [Fact]
    public void GetLayer_ByType_ReturnsTheSingleMatch()
    {
        var camera = new CameraLayer2D();
        var scene = SceneWith(camera, new PlayField2D());

        Assert.Same(camera, scene.GetEntity<CameraLayer2D>());
    }

    [Fact]
    public void GetLayer_ByName_ReturnsNamedLayer()
    {
        var hud = new CameraLayer2D { Name = "hud" };
        var world = new CameraLayer2D { Name = "world" };
        var scene = SceneWith(hud, world);

        Assert.Same(world, scene.GetEntity<CameraLayer2D>("world"));
    }

    [Fact]
    public void GetLayer_ByType_AmbiguousThrows()
    {
        var scene = SceneWith(new CameraLayer2D(), new CameraLayer2D());
        Assert.Throws<InvalidOperationException>(() => scene.GetEntity<CameraLayer2D>());
    }

    [Fact]
    public void TryGetEntity_ByName_MissingReturnsFalse()
    {
        var scene = SceneWith(new CameraLayer2D { Name = "main" });
        Assert.False(scene.TryGetEntity<CameraLayer2D>("nope", out var layer));
        Assert.Null(layer);
    }

    [Fact]
    public void GetLayer_ByName_WrongTypeThrows()
    {
        var scene = SceneWith(new CameraLayer2D { Name = "main" });
        Assert.Throws<InvalidOperationException>(() => scene.GetEntity<PlayField2D>("main"));
    }

    [Fact]
    public void CameraFollow2D_ResolvesCameraByType()
    {
        var cameraLayer = new CameraLayer2D();
        var sprite = new Sprite2D();
        var playfield = new PlayField2D();
        var follow = sprite.GetOrAddBehavior<CameraFollow2D>();
        playfield.AddEntity(sprite);
        SceneWith(cameraLayer, playfield);

        Updater.Default.UpdateEntity(sprite, new EntityUpdateContext());

        Assert.Same(cameraLayer.Camera, follow.Camera);
    }

    [Fact]
    public void CameraFollow2D_ResolvesCameraByName()
    {
        var world = new CameraLayer2D { Name = "world" };
        var hud = new CameraLayer2D { Name = "hud" };
        var sprite = new Sprite2D();
        var playfield = new PlayField2D();
        var follow = sprite.GetOrAddBehavior<CameraFollow2D>();
        follow.CameraName = "world";
        playfield.AddEntity(sprite);
        SceneWith(world, hud, playfield);

        Updater.Default.UpdateEntity(sprite, new EntityUpdateContext());

        Assert.Same(world.Camera, follow.Camera);
    }

    [Fact]
    public void CameraFollow2D_ExplicitCameraWinsOverResolution()
    {
        var cameraLayer = new CameraLayer2D();
        var explicitCamera = new Camera2D();
        var sprite = new Sprite2D();
        var playfield = new PlayField2D();
        var follow = sprite.GetOrAddBehavior<CameraFollow2D>();
        follow.Camera = explicitCamera;
        playfield.AddEntity(sprite);
        SceneWith(cameraLayer, playfield);

        Updater.Default.UpdateEntity(sprite, new EntityUpdateContext());

        Assert.Same(explicitCamera, follow.Camera);
    }
}
