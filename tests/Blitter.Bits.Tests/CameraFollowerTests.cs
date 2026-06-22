using System.Numerics;

using Blitter.Bits;

namespace Blitter.Tests;

public class CameraFollowerTests
{
    private static UpdateContext Ctx(double dtSeconds, double totalSeconds = 0) =>
        new()
        {
            ElapsedSinceStart = TimeSpan.FromSeconds(totalSeconds),
            ElapsedSinceLastUpdate = TimeSpan.FromSeconds(dtSeconds),
        };

    [Fact]
    public void FirstUpdate_SnapsToGoalPose()
    {
        var f = new CameraFollower
        {
            Target = new Vector3(10f, 0f, 0f),
            Offset = new Vector3(0f, 2f, 5f),
        };
        f.Update(Ctx(0.016));

        Assert.Equal(new Vector3(10f, 2f, 5f), f.Camera.Position);
        Assert.Equal(new Vector3(10f, 0f, 0f), f.Camera.Target);
    }

    [Fact]
    public void Update_PropagatesUpToCamera()
    {
        var f = new CameraFollower { Up = new Vector3(0f, 0f, 1f) };
        f.Update(Ctx(0.016));
        Assert.Equal(new Vector3(0f, 0f, 1f), f.Camera.Up);
    }

    [Fact]
    public void ZeroSmoothing_SnapsToNewGoalEachFrame()
    {
        var f = new CameraFollower
        {
            Offset = new Vector3(0f, 0f, 5f),
            PositionSmoothing = 0f,
            LookSmoothing = 0f,
        };
        f.Update(Ctx(0.016)); // initialize at origin

        f.Target = new Vector3(100f, 0f, 0f);
        f.Update(Ctx(0.016));

        Assert.Equal(new Vector3(100f, 0f, 5f), f.Camera.Position);
        Assert.Equal(new Vector3(100f, 0f, 0f), f.Camera.Target);
    }

    [Fact]
    public void PositiveSmoothing_MovesPartwayTowardGoal()
    {
        var f = new CameraFollower
        {
            Offset = new Vector3(0f, 0f, 5f),
            PositionSmoothing = 0.25f,
        };
        f.Update(Ctx(0.016)); // initialize: position (0,0,5)

        f.Target = new Vector3(100f, 0f, 0f);
        f.Update(Ctx(0.016));

        // Eased toward (100,0,5) but nowhere near arrived in one frame.
        Assert.True(f.Camera.Position.X > 0f);
        Assert.True(f.Camera.Position.X < 100f);
    }

    [Fact]
    public void Snap_ThenUpdate_HoldsGoalPose()
    {
        var f = new CameraFollower
        {
            Target = new Vector3(5f, 0f, 0f),
            Offset = new Vector3(0f, 0f, 5f),
            PositionSmoothing = 0.25f,
        };
        f.Snap();
        f.Update(Ctx(0.5));

        Assert.Equal(new Vector3(5f, 0f, 5f), f.Camera.Position);
        Assert.Equal(new Vector3(5f, 0f, 0f), f.Camera.Target);
    }
}
