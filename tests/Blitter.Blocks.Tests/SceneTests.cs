namespace Blitter.Blocks.Tests;

public class SceneTests
{
    private sealed class FakeLayer : Layer2D
    {
        public int UpdateCount { get; private set; }
        public int RenderCount { get; private set; }

        public override void Update(in UpdateContext2D context)
        {
            UpdateCount++;
        }

        public override void Draw(Renderer2D renderer)
        {
            RenderCount++;
        }
    }

    [Fact]
    public void Update_VisitsEveryLayer()
    {
        var a = new FakeLayer();
        var b = new FakeLayer();
        var scene = new Scene2D(a, b);

        scene.Update(new UpdateContext2D());

        Assert.Equal(1, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }

    [Fact]
    public void Update_SkipsDisabledLayer()
    {
        var a = new FakeLayer { Enabled = false };
        var b = new FakeLayer();
        var scene = new Scene2D(a, b);

        scene.Update(new UpdateContext2D());

        Assert.Equal(0, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }

    [Fact]
    public void AddLayer_ExposesAddedLayerToFutureUpdates()
    {
        var initial = new FakeLayer();
        var scene = new Scene2D(initial);

        var added = new FakeLayer();
        scene.AddLayer(added);

        scene.Update(new UpdateContext2D());

        Assert.Equal(1, initial.UpdateCount);
        Assert.Equal(1, added.UpdateCount);
    }

    [Fact]
    public void RemoveLayer_StopsTickingRemovedLayer()
    {
        var a = new FakeLayer();
        var b = new FakeLayer();
        var scene = new Scene2D(a, b);

        scene.RemoveLayer(a);
        scene.Update(new UpdateContext2D());

        Assert.Equal(0, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }
}
