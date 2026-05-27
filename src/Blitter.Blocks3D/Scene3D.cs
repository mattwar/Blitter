namespace Blitter.Blocks3D;

/// <summary>
/// A 3D scene comprised of one or more layers. The 3D analog of
/// <c>Blitter.Blocks2D.Scene2D</c>.
/// </summary>
public class Scene3D
{
    public Scene3D()
    {
    }

    /// <summary>
    /// The layers in this scene. Each layer's <see cref="Layer3D.Update"/>
    /// runs in list order, then each <see cref="Layer3D.Draw"/> renders in
    /// list order — there is no parallax/back-to-front compositing in 3D;
    /// the depth buffer sorts geometry naturally.
    /// </summary>
    public List<Layer3D> Layers { get; } = new();

    /// <summary>Scene-wide behaviors that run each frame before layers update.</summary>
    public List<SceneBehavior3D> Behaviors { get; } = new();

    internal void Update(in UpdateContext3D context)
    {
        foreach (var behavior in Behaviors)
        {
            if (behavior.Enabled)
                behavior.Apply(this, in context);
        }

        foreach (var layer in Layers)
        {
            if (layer.Enabled)
                layer.Update(in context);
        }

        if (RunState == RunState.Exiting)
        {
            for (int i = _exitConditions.Count - 1; i >= 0; i--)
            {
                if (_exitConditions[i](context))
                    _exitConditions.RemoveAt(i);
            }
            if (_exitConditions.Count == 0)
                RunState = RunState.Exited;
        }
    }

    internal void Draw(Renderer3D renderer)
    {
        foreach (var layer in Layers)
        {
            if (layer.Visible)
                layer.Draw(renderer);
        }
    }

    /// <summary>
    /// Runs the scene until an exit condition is met, the caller-supplied
    /// <paramref name="shouldExit"/> returns true, the window is closed,
    /// or the cancellation token fires.
    /// </summary>
    public Task RunAsync(
        Window3D window,
        Func<Scene3D, bool> shouldExit,
        CancellationToken cancellationToken = default) =>
        AnimateAsync(window, shouldExit, cancellationToken);

    /// <summary>
    /// Runs the scene until an exit condition is met, the window is
    /// closed, or the cancellation token fires.
    /// </summary>
    public Task RunAsync(
        Window3D window,
        CancellationToken cancellationToken = default) =>
        AnimateAsync(window, null, cancellationToken);

    private async Task AnimateAsync(
        Window3D window,
        Func<Scene3D, bool>? shouldExit,
        CancellationToken cancellationToken)
    {
        _exitConditions.Clear();
        RunState = RunState.Running;

        await window.RunAsync(
            shouldExit: () => RunState == RunState.Exited || (shouldExit?.Invoke(this) ?? false),
            renderFrame: rd =>
            {
                var context = rd.GetUpdateContext();
                this.Update(in context);
                this.Draw(rd);
            },
            cancellationToken);
    }

    /// <summary>
    /// Current run-loop state. Becomes <see cref="RunState.Exiting"/>
    /// after the first call to <see cref="Exit"/> or
    /// <see cref="ExitWithDelay(SceneExitCondition3D)"/>, and
    /// <see cref="RunState.Exited"/> once all exit conditions have fired.
    /// </summary>
    public RunState RunState { get; private set; }

    private readonly List<SceneExitCondition3D> _exitConditions = new();

    /// <summary>
    /// Requests the scene to exit, but not until the predicate returns true.
    /// </summary>
    public void ExitWithDelay(SceneExitCondition3D until)
    {
        ArgumentNullException.ThrowIfNull(until);
        if (RunState == RunState.Exited)
            return;
        _exitConditions.Add(until);
        if (RunState == RunState.Running)
            RunState = RunState.Exiting;
    }

    /// <summary>
    /// Requests the scene to exit, but not until the specified duration has elapsed.
    /// </summary>
    public void ExitWithDelay(TimeSpan duration)
    {
        TimeSpan? deadline = null;
        ExitWithDelay((in ctx) =>
        {
            deadline ??= ctx.ElapsedSinceStart + duration;
            return ctx.ElapsedSinceStart >= deadline.Value;
        });
    }

    /// <summary>Requests immediate exit.</summary>
    public void Exit() => ExitWithDelay(static (in _) => true);
}

/// <summary>
/// A predicate that determines when a <see cref="Scene3D"/> should truly exit.
/// </summary>
public delegate bool SceneExitCondition3D(in UpdateContext3D context);
