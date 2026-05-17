using System.Collections.Immutable;

namespace Blitter.Blocks;

/// <summary>
/// Lifecycle state of a <see cref="Scene2D"/>'s run loop.
/// </summary>
public enum RunState
{
    /// <summary>The scene is running normally.</summary>
    Running,
    /// <summary>Exit has been requested; the scene keeps advancing until all exit conditions are met.</summary>
    Exiting,
    /// <summary>All exit conditions have been met; the run loop has stopped (or will on its next check).</summary>
    Exited,
}

/// <summary>
/// A 2D scene comprised of one or more layers.
/// </summary>
public class Scene2D
{
    private ImmutableList<Layer2D> _layers;
    private Window2D? _window;

    public Scene2D()
    {
        _layers = ImmutableList<Layer2D>.Empty;
    }

    public Scene2D(IEnumerable<Layer2D> layers)
    {
        _layers = AdoptAll(layers);
    }

    public Scene2D(params Layer2D[] layers)
    {
        _layers = AdoptAll(layers);
    }

    private ImmutableList<Layer2D> AdoptAll(IEnumerable<Layer2D> layers)
    {
        var list = layers.ToImmutableList();
        foreach (var layer in list)
        {
            layer._scene?.RemoveLayer(layer);
            layer._scene = this;
        }
        return list;
    }

    /// <summary>The layers in this scene, back-to-front.</summary>
    public ImmutableList<Layer2D> Layers => _layers;

    /// <summary>
    /// Add a new layer to the scene.
    /// This new layer becomes the topmost layer.
    /// </summary>
    public void AddLayer(Layer2D layer)
    {
        var existing = layer._scene;
        if (existing == this)
            return;
        existing?.RemoveLayer(layer);
        ImmutableInterlocked.Update(ref _layers, (list, l) => list.Add(l), layer);
        layer._scene = this;
    }

    /// <summary>
    /// Inserts <paramref name="layer"/> at <paramref name="index"/>.
    /// Lower indexes render first (further back).
    /// </summary>
    public void InsertLayer(int index, Layer2D layer)
    {
        var existing = layer._scene;
        if (existing != null && existing != this)
            existing.RemoveLayer(layer);
        ImmutableInterlocked.Update(ref _layers, (list, args) => list.Insert(args.index, args.layer), (index, layer));
        layer._scene = this;
    }

    /// <summary>
    /// Removes a layer from the scene.
    /// </summary>
    public void RemoveLayer(Layer2D layer)
    {
        ImmutableInterlocked.Update(ref _layers, (list, l) => list.Remove(l), layer);
        if (layer._scene == this)
            layer._scene = null;
    }

    /// <summary>
    /// Scene-wide behaviors that run each tick before layers update.
    /// </summary>
    public List<SceneBehavior2D> Behaviors { get; } = new();

    /// <summary>
    /// Runs scene behaviors, updates all enabled layers, then evaluates any
    /// pending exit conditions.
    /// </summary>
    internal void Update(in UpdateContext2D context)
    {
        foreach (var behavior in Behaviors)
        {
            if (behavior.Enabled)
                behavior.Update(this, in context);
        }

        var layers = _layers;
        foreach (var layer in layers)
        {
            if (layer.Enabled)
                layer.Update(in context);
        }

        if (RunState == RunState.Exiting)
        {
            // Iterate back-to-front so RemoveAt is safe and we can't capture
            // the `in` context in a lambda.
            for (int i = _exitConditions.Count - 1; i >= 0; i--)
            {
                if (_exitConditions[i](context))
                    _exitConditions.RemoveAt(i);
            }
            if (_exitConditions.Count == 0)
                RunState = RunState.Exited;
        }
    }

    /// <summary>
    /// Draws all visible layers in the scene.
    /// </summary>
    internal void Draw(Renderer2D renderer)
    {
        var layers = _layers;
        foreach (var layer in layers)
        {
            if (layer.Visible)
                layer.Draw(renderer);
        }
    }

    /// <summary>
    /// Runs the scene until an exit condition is met, the caller-supplied
    /// <paramref name="shouldExit"/> returns true, the window is closed, or
    /// the cancellation token fires.
    /// </summary>
    public Task RunAsync(
        Window2D window,
        Func<Scene2D, bool> shouldExit,
        CancellationToken cancellationToken = default)
    {
        return AnimateAsync(window, shouldExit, cancellationToken);
    }

    /// <summary>
    /// Runs the scene until an exit condition is met, the window is closed,
    /// or the cancellation token fires.
    /// </summary>
    public Task RunAsync(
        Window2D window,
        CancellationToken cancellationToken = default)
    {
        return AnimateAsync(window, null, cancellationToken);
    }

    private async Task AnimateAsync(
        Window2D window,
        Func<Scene2D, bool>? shouldExit,
        CancellationToken cancellationToken)
    {
        _window = window;
        _exitConditions.Clear();
        RunState = RunState.Running;

        try
        {
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
        finally
        {
            _window = null;
        }
    }

    /// <summary>
    /// Current run-loop state. Becomes <see cref="RunState.Exiting"/> after
    /// the first call to <see cref="Exit"/> or <see cref="ExitWithDelay(SceneExitCondition)"/>,
    /// and <see cref="RunState.Exited"/> once all exit conditions have fired.
    /// </summary>
    public RunState RunState { get; private set; }

    private readonly List<SceneExitCondition> _exitConditions = new();

    /// <summary>
    /// Requests the scene to exit, but not until the predicate returns true.
    /// The <see cref="p:RunState"/> transitions to <see cref="RunState.Exiting"/> immediately 
    /// and remains in that state until all the other exit conditions have been met.
    /// Note, some exit conditions like window close cause an immediate exit.
    /// </summary>
    public void ExitWithDelay(SceneExitCondition until)
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
    /// The <see cref="p:RunState"/> transitions to <see cref="RunState.Exiting"/> immediately 
    /// and remains in that state until the specified duration has passed, 
    /// and all the other exit conditions have been met.
    /// Note, some exit conditions like window close cause an immediate exit.
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

    /// <summary>
    /// Requests immediate exit.
    /// The <see cref="p:RunState"/> transitions to <see cref="RunState.Exiting"/> immediately 
    /// and remains in that state until all the other exit conditions have been met.
    /// Note, some exit conditions like window close cause an immediate exit.
    /// </summary>
    public void Exit() => ExitWithDelay(static (in _) => true);
}

/// <summary>
/// A predicate that determines when a <see cref="Scene2D"/> should truly exit
/// </summary>
public delegate bool SceneExitCondition(in UpdateContext2D context);