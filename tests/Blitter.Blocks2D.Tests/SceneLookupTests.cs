namespace Blitter.Blocks2D.Tests;

public class SceneLookupTests
{
    // Wires layers' Scene back-references the way the scene's attach walk
    // does, without needing a window/renderer.
    private static Scene2D SceneWith(params Layer2D[] layers)
    {
        var scene = new Scene2D();
        foreach (var layer in layers)
        {
            scene.Layers.Add(layer);
            layer._scene = scene;
        }
        return scene;
    }

    [Fact]
    public void GetLayer_ByType_ReturnsTheSingleMatch()
    {
        var camera = new CameraLayer2D();
        var scene = SceneWith(camera, new PlayField2D());

        Assert.Same(camera, scene.GetLayer<CameraLayer2D>());
    }

    [Fact]
    public void GetLayer_ByName_ReturnsNamedLayer()
    {
        var hud = new CameraLayer2D { Name = "hud" };
        var world = new CameraLayer2D { Name = "world" };
        var scene = SceneWith(hud, world);

        Assert.Same(world, scene.GetLayer<CameraLayer2D>("world"));
    }

    [Fact]
    public void GetLayer_ByType_AmbiguousThrows()
    {
        var scene = SceneWith(new CameraLayer2D(), new CameraLayer2D());
        Assert.Throws<InvalidOperationException>(() => scene.GetLayer<CameraLayer2D>());
    }

    [Fact]
    public void TryGetLayer_ByName_MissingReturnsFalse()
    {
        var scene = SceneWith(new CameraLayer2D { Name = "main" });
        Assert.False(scene.TryGetLayer<CameraLayer2D>("nope", out var layer));
        Assert.Null(layer);
    }

    [Fact]
    public void GetLayer_ByName_WrongTypeThrows()
    {
        var scene = SceneWith(new CameraLayer2D { Name = "main" });
        Assert.Throws<InvalidOperationException>(() => scene.GetLayer<PlayField2D>("main"));
    }

    [Fact]
    public void PlayField_GetSprite_ResolvesLocally()
    {
        var sprite = new Sprite2D { Name = "hero" };
        var playfield = new PlayField2D();
        playfield.AddSprite(sprite);

        Assert.Same(sprite, playfield.GetSprite("hero"));
        Assert.False(playfield.TryGetSprite("ghost", out var ghost));
        Assert.Null(ghost);
    }

    [Fact]
    public void PlayField_GetSprite_WrongTypeThrows()
    {
        var sprite = new Sprite2D { Name = "hero" };
        var playfield = new PlayField2D();
        playfield.AddSprite(sprite);

        Assert.Throws<InvalidOperationException>(() => playfield.GetSprite<NamedSprite>("hero"));
    }

    [Fact]
    public void CameraFollow2D_ResolvesCameraByType()
    {
        var cameraLayer = new CameraLayer2D();
        var follow = new CameraFollow2D();
        var sprite = new Sprite2D();
        var playfield = new PlayField2D();
        sprite.Behaviors.Add(follow);
        playfield.AddSprite(sprite);
        SceneWith(cameraLayer, playfield);

        follow.OnAttach(sprite);

        Assert.Same(cameraLayer.Camera, follow.Camera);
    }

    [Fact]
    public void CameraFollow2D_ResolvesCameraByName()
    {
        var world = new CameraLayer2D { Name = "world" };
        var hud = new CameraLayer2D { Name = "hud" };
        var follow = new CameraFollow2D { CameraName = "world" };
        var sprite = new Sprite2D();
        var playfield = new PlayField2D();
        sprite.Behaviors.Add(follow);
        playfield.AddSprite(sprite);
        SceneWith(world, hud, playfield);

        follow.OnAttach(sprite);

        Assert.Same(world.Camera, follow.Camera);
    }

    [Fact]
    public void CameraFollow2D_ExplicitCameraWinsOverResolution()
    {
        var cameraLayer = new CameraLayer2D();
        var explicitCamera = new Camera2D();
        var follow = new CameraFollow2D { Camera = explicitCamera };
        var sprite = new Sprite2D();
        var playfield = new PlayField2D();
        sprite.Behaviors.Add(follow);
        playfield.AddSprite(sprite);
        SceneWith(cameraLayer, playfield);

        follow.OnAttach(sprite);

        Assert.Same(explicitCamera, follow.Camera);
    }

    private sealed class NamedSprite : Sprite2D
    {
    }
}
