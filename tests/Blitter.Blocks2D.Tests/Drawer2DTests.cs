using System.Numerics;

namespace Blitter.Blocks2D.Tests;

public class Drawer2DTests
{
    [Fact]
    public void Draw_WalksDrawableEntitiesAndBehaviors()
    {
        var calls = new List<string>();
        var root = new Container
        {
            Behaviors = [new TestDrawableBehavior("root behavior", calls)],
            Entities = [new TestDrawableEntity("child", calls)]
        };
        root.Entities[0].GetOrAddBehavior<TestDrawableBehavior>().Configure("child behavior", calls);

        Drawer2D.Default.Draw(root, null!);

        Assert.Equal(["root behavior", "child", "child behavior"], calls);
    }

    [Fact]
    public void Draw_DoesNotWalkChildrenOfDrawableEntity()
    {
        var calls = new List<string>();
        var root = new TestDrawableContainer("root", calls)
        {
            Entities = [new TestDrawableEntity("child", calls)]
        };

        Drawer2D.Default.Draw(root, null!);

        Assert.Equal(["root"], calls);
    }

    [Fact]
    public void Draw_SkipsInvisibleSubtree()
    {
        var calls = new List<string>();
        var root = new TestVisibleContainer { Visible = false, Entities = [new TestDrawableEntity("child", calls)] };

        Drawer2D.Default.Draw(root, null!);

        Assert.Empty(calls);
    }

    [Fact]
    public void Draw_AppliesDrawableSetupBeforeEntityAndBehaviorDraw()
    {
        var calls = new List<string>();
        var root = new TestCameraDrawableEntity("root", calls)
        {
            Behaviors =
            [
                new Parallax2D { Factor = new Vector2(0.1f, 0.1f) },
                new TestCameraDrawableBehavior("behavior", calls),
            ],
        };
        var window = new Window2D { LogicalSize = (64, 64) };
        var camera = new Camera2D { Position = new Vector2(100f, 40f) };
        window.Renderer.Camera = camera;

        Drawer2D.Default.Draw(root, window.Renderer);

        Assert.Equal(["root:10,4", "behavior:10,4"], calls);
        Assert.Same(camera, window.Renderer.Camera);
    }

    [Fact]
    public void Draw_AppliesDrawableSetupAroundChildTraversal()
    {
        var calls = new List<string>();
        var root = new Container
        {
            Behaviors = [new Parallax2D { Factor = new Vector2(0.1f, 0.1f) }],
            Entities = [new TestCameraDrawableEntity("child", calls)],
        };
        var window = new Window2D { LogicalSize = (64, 64) };
        var camera = new Camera2D { Position = new Vector2(100f, 40f) };
        window.Renderer.Camera = camera;

        Drawer2D.Default.Draw(root, window.Renderer);

        Assert.Equal(["child:10,4"], calls);
        Assert.Same(camera, window.Renderer.Camera);
    }

    private sealed class TestDrawableEntity(string name, List<string> calls) : Entity, IDrawable2D
    {
        public void Draw(Renderer2D renderer) => calls.Add(name);
    }

    private sealed class TestDrawableContainer(string name, List<string> calls) : Container, IDrawable2D
    {
        public void Draw(Renderer2D renderer) => calls.Add(name);
    }

    private sealed class TestVisibleContainer : Container, IVisibility
    {
        public bool Visible { get; init; } = true;
    }

    private sealed class TestCameraDrawableEntity(string name, List<string> calls) : Entity, IDrawable2D
    {
        public void Draw(Renderer2D renderer)
        {
            var position = renderer.Camera?.Position ?? Vector2.Zero;
            calls.Add($"{name}:{position.X},{position.Y}");
        }
    }

    private sealed class TestCameraDrawableBehavior(string name, List<string> calls) : Behavior, IDrawable2D
    {
        public void Draw(Renderer2D renderer)
        {
            var position = renderer.Camera?.Position ?? Vector2.Zero;
            calls.Add($"{name}:{position.X},{position.Y}");
        }
    }

    private sealed class TestDrawableBehavior : Behavior, IDrawable2D
    {
        private string _name = "";
        private List<string> _calls = null!;

        public TestDrawableBehavior()
        {
        }

        public TestDrawableBehavior(string name, List<string> calls)
        {
            Configure(name, calls);
        }

        public void Configure(string name, List<string> calls)
        {
            _name = name;
            _calls = calls;
        }

        public void Draw(Renderer2D renderer) => _calls.Add(_name);
    }
}
