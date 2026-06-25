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

    private sealed class TestDrawableEntity(string name, List<string> calls) : Entity, IDrawable2D
    {
        public void Draw(Renderer2D renderer) => calls.Add(name);
    }

    private sealed class TestDrawableContainer(string name, List<string> calls) : Container, IDrawable2D
    {
        public void Draw(Renderer2D renderer) => calls.Add(name);
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
