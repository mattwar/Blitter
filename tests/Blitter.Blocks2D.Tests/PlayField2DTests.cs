namespace Blitter.Blocks2D.Tests;

public class PlayField2DTests
{
    private sealed class ReRoleAsBarrierOnUpdate(PlayField2D playfield) : Behavior, IUpdatable
    {
        public Containment ContainmentDuringUpdate { get; private set; }

        public void Update(in UpdateContext context)
        {
            playfield.AddBarrier(Entity!);
            ContainmentDuringUpdate = playfield.GetContainment(Entity!);
        }
    }

    [Fact]
    public void AddSprite_AcceptsPlainEntity()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();

        playfield.AddSprite(entity);

        Assert.Same(entity, Assert.Single(playfield.Sprites));
        Assert.Same(playfield, entity.Parent);
        Assert.True(playfield.TryGetSprite<Entity>(out var found));
        Assert.Same(entity, found);
        Assert.Equal(Containment.Contained, playfield.GetContainment(entity));

        playfield.RemoveSprite(entity);

        Assert.Empty(playfield.Sprites);
        Assert.Null(entity.Parent);
        Assert.Equal(Containment.NotContained, playfield.GetContainment(entity));
    }

    [Fact]
    public void AddBarrier_AcceptsPlainEntity()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();

        playfield.AddBarrier(entity);

        Assert.Same(entity, Assert.Single(playfield.Barriers));
        Assert.Same(playfield, entity.Parent);
        Assert.Equal(Containment.Contained, playfield.GetContainment(entity));

        playfield.RemoveBarrier(entity);

        Assert.Empty(playfield.Barriers);
        Assert.Null(entity.Parent);
        Assert.Equal(Containment.NotContained, playfield.GetContainment(entity));
    }

    [Fact]
    public void AddSprite_RemovesExistingBarrierRole()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();

        playfield.AddBarrier(entity);
        playfield.AddSprite(entity);

        Assert.Empty(playfield.Barriers);
        Assert.Same(entity, Assert.Single(playfield.Sprites));
        Assert.Same(playfield, entity.Parent);
        Assert.Equal(Containment.Contained, playfield.GetContainment(entity));
    }

    [Fact]
    public void AddBarrier_RemovesExistingSpriteRole()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();

        playfield.AddSprite(entity);
        playfield.AddBarrier(entity);

        Assert.Empty(playfield.Sprites);
        Assert.Same(entity, Assert.Single(playfield.Barriers));
        Assert.Same(playfield, entity.Parent);
        Assert.Equal(Containment.Contained, playfield.GetContainment(entity));
    }

    [Fact]
    public void RemoveEntity_RemovesFromEitherRole()
    {
        var playfield = new PlayField2D();
        var sprite = new Entity();
        var barrier = new Entity();

        playfield.AddSprite(sprite);
        playfield.AddBarrier(barrier);

        playfield.RemoveEntity(sprite);
        playfield.RemoveEntity(barrier);

        Assert.Empty(playfield.Sprites);
        Assert.Empty(playfield.Barriers);
        Assert.Null(sprite.Parent);
        Assert.Null(barrier.Parent);
    }

    [Fact]
    public void AddBarrier_DuringUpdate_ReRolesSpriteAsContainedBarrier()
    {
        var playfield = new PlayField2D();
        var entity = new Entity();
        var reRole = new ReRoleAsBarrierOnUpdate(playfield);
        entity.AddBehavior(reRole);
        playfield.AddSprite(entity);

        playfield.Update(new UpdateContext());

        Assert.Equal(Containment.Contained, reRole.ContainmentDuringUpdate);
        Assert.Empty(playfield.Sprites);
        Assert.Same(entity, Assert.Single(playfield.Barriers));
        Assert.Same(playfield, entity.Parent);
    }
}