using System.Numerics;

namespace Blitter.Tests;

public class ContainmentTests
{
    private static UpdateContext Ctx() => new()
    {
        ElapsedSinceStart = TimeSpan.Zero,
        ElapsedSinceLastUpdate = TimeSpan.FromSeconds(0.016),
    };

    [Fact]
    public void Sprite_AfterAdd_IsContained()
    {
        var field = new PlayField3D();
        var sprite = new Sprite3D();
        field.AddSprite(sprite);

        Assert.Equal(Containment.Contained, field.GetContainment(sprite));
    }

    [Fact]
    public void Sprite_NeverAdded_IsNotContained()
    {
        var field = new PlayField3D();
        var sprite = new Sprite3D();

        Assert.Equal(Containment.NotContained, field.GetContainment(sprite));
    }

    [Fact]
    public void Sprite_RemovedMidFrame_IsRemovingThenReaped()
    {
        var field = new PlayField3D();
        var observer = new SelfRemove();
        var sprite = new Sprite3D { Behaviors = [observer] };
        field.AddSprite(sprite);

        field.Update(Ctx());

        // Observed inside the frame, right after the kill request.
        Assert.Equal(Containment.Removing, observer.Observed);
        // Reaped at end of frame.
        Assert.Equal(Containment.NotContained, field.GetContainment(sprite));
    }

    [Fact]
    public void Barrier_AfterAdd_IsContained()
    {
        var field = new PlayField3D();
        var barrier = new WallBarrier3D(
            Vector3.Zero, Vector3.UnitY, Vector3.UnitX, new Vector2(1f, 1f));
        field.AddBarrier(barrier);

        Assert.Equal(Containment.Contained, field.GetContainment(barrier));
    }

    [Fact]
    public void Scene_Layer_IsContained()
    {
        var field = new PlayField3D();
        var scene = new Scene3D { Layers = [field] };

        Assert.Equal(Containment.Contained, scene.GetContainment(field));
    }

    [Fact]
    public void Entity_Default_IsNotContained()
    {
        var parent = new Sprite3D();
        var child = new Sprite3D();

        Assert.Equal(Containment.NotContained, parent.GetContainment(child));
    }

    // Removes its own sprite from the field during update and records the
    // containment state observed immediately after the kill request.
    private sealed class SelfRemove : Behavior
    {
        public Containment Observed { get; private set; }

        public override void Apply(in UpdateContext context)
        {
            var sprite = (Sprite3D)Entity;
            var field = (PlayField3D)sprite.Parent!;
            field.RemoveSprite(sprite);
            Observed = field.GetContainment(sprite);
        }
    }
}
