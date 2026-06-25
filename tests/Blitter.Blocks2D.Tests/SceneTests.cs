namespace Blitter.Blocks2D.Tests;

public class SceneTests
{
    private sealed class FakeLayer : Layer2D, IUpdatable
    {
        public int UpdateCount { get; private set; }
        public int RenderCount { get; private set; }

        public void Update(in EntityUpdateContext context)
        {
            UpdateCount++;
        }

        protected override void DrawContent(Renderer2D renderer)
        {
            RenderCount++;
        }
    }

    [Fact]
    public void Update_VisitsEveryLayer()
    {
        var a = new FakeLayer();
        var b = new FakeLayer();
        var scene = new Scene2D { Entities = [a, b] };

        Updater.Default.Update(scene, new EntityUpdateContext());

        Assert.Equal(1, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }

    [Fact]
    public void Update_SkipsDisabledLayer()
    {
        var a = new FakeLayer { Enabled = false };
        var b = new FakeLayer();
        var scene = new Scene2D { Entities = [a, b] };

        Updater.Default.Update(scene, new EntityUpdateContext());

        Assert.Equal(0, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }

    [Fact]
    public void Layers_AddAfterConstructionTicksTheLayer()
    {
        var initial = new FakeLayer();
        var scene = new Scene2D { Entities = [initial] };

        var added = new FakeLayer();
        scene.AddEntity(added);

        Updater.Default.Update(scene, new EntityUpdateContext());

        Assert.Equal(1, initial.UpdateCount);
        Assert.Equal(1, added.UpdateCount);
    }

    [Fact]
    public void Layers_RemoveStopsTickingTheLayer()
    {
        var a = new FakeLayer();
        var b = new FakeLayer();
        var scene = new Scene2D { Entities = [a, b] };

        scene.RemoveEntity(a);
        Updater.Default.Update(scene, new EntityUpdateContext());

        Assert.Equal(0, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }
}
