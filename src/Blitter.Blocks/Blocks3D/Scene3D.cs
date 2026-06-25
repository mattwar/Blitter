namespace Blitter.Blocks3D;

/// <summary>
/// A 3D scene comprised of one or more layers. 
/// The 3D analog of <c>Blitter.Blocks2D.Scene2D</c>.
/// </summary>
public class Scene3D : Entity, IContainerEntity
{
    public Scene3D()
    {
    }

    private readonly List<Layer3D> _layers = new();

    /// <summary>
    /// The layers in this scene.
    /// </summary>
    public IReadOnlyList<Layer3D> Layers 
    { 
        get => _layers;
        init
        {
            if (_layers.Count > 0)
            {
                // we may have a collision during initialization.
                foreach (var layer in value)
                {
                    int i = _layers.FindIndex(existing => existing.GetType() == layer.GetType());
                    if (i >= 0)
                    {
                        // let new value win
                        _layers[i] = layer;
                    }
                    else
                    {
                        _layers.Add(layer);
                    }
                }
            }
            else
            {
                _layers.AddRange(value);
            }            
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEntity> Entities => _layers;

    /// <inheritdoc/>
    public void AddEntity(IEntity child)
    {
        if (child is not Layer3D layer)
            throw new InvalidOperationException("Scene3D can only contain Layer3D entities.");
        _layers.Add(layer);
        layer.Container = this;
    }

    /// <summary>
    /// 3D scenes do not track layer age; always <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan GetAge(IEntity child) => TimeSpan.Zero;

    /// <summary>
    /// Removes <paramref name="child"/> from the scene when it is one of its layers.
    /// No-op otherwise.
    /// </summary>
    public void RemoveEntity(IEntity child)
    {
        if (child is Layer3D layer && _layers.Remove(layer))
            layer.Container = null;
    }

    /// <summary>
    /// Reports whether <paramref name="child"/> is a layer this scene holds.
    /// </summary>
    public Containment GetContainment(IEntity child) =>
        child is Layer3D layer && _layers.Contains(layer)
            ? Containment.Contained
            : Containment.NotContained;

    public override void Update(in UpdateContext context)
    {
        foreach (var behavior in Behaviors)
        {
            if (behavior is IUpdatable updatable)
                updatable.Update(in context);
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
public delegate bool SceneExitCondition3D(in UpdateContext context);
