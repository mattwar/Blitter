using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Blitter.Blocks2D;

/// <summary>
/// A 2D scene comprised of one or more layers.
/// </summary>
public class Scene2D
{
    private Window2D? _window;

    public Scene2D()
    {
    }

    /// <summary>
    /// The layers in this scene, back-to-front.
    /// </summary>
    public List<Layer2D> Layers { get; } = new();

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
                behavior.Apply(this, in context);
        }

        foreach (var layer in Layers)
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
        foreach (var layer in Layers)
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

        Attach();

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
    /// Walks the fully-built scene tree once, before the first frame: wires
    /// each layer's <see cref="Layer2D.Scene"/> back-reference, then calls
    /// each node's <c>OnAttach</c> hook. Two passes so the back-references
    /// (and thus cross-node lookups) are in place before any hook runs. Each
    /// layer attaches its own contents (a <see cref="PlayField2D"/> recurses
    /// into its sprites and their behaviors).
    /// </summary>
    private void Attach()
    {
        // Pass 1: wire back-references so Scene/PlayField navigation works.
        foreach (var layer in Layers)
            layer._scene = this;

        // Pass 2: run the attach hooks now that the graph is navigable.
        foreach (var behavior in Behaviors)
            behavior.OnAttach(this);

        foreach (var layer in Layers)
            layer.OnAttach();
    }

    /// <summary>
    /// The renderer this scene draws through. Available only while the
    /// scene is running (including during <c>OnAttach</c>).
    /// </summary>
    public Renderer2D Renderer =>
        _window?.Renderer ?? throw new InvalidOperationException("The scene's renderer is available only while the scene is running.");

    /// <summary>
    /// Tries to resolve the single layer assignable to <typeparamref name="T"/>.
    /// Returns <c>false</c> if none. Throws if more than one matches (name it
    /// and use <see cref="TryGetLayer{T}(string, out T)"/> to disambiguate).
    /// </summary>
    public bool TryGetLayer<T>([NotNullWhen(true)] out T? layer) where T : class
    {
        T? match = null;
        foreach (var candidate in Layers)
        {
            if (candidate is not T typed)
                continue;
            if (match is not null)
                throw new InvalidOperationException($"More than one layer is a {typeof(T).Name}; resolve it by name instead.");
            match = typed;
        }
        layer = match;
        return match is not null;
    }

    /// <summary>
    /// Resolves the single layer assignable to <typeparamref name="T"/>.
    /// Throws if none exists or more than one matches.
    /// </summary>
    public T GetLayer<T>() where T : class =>
        TryGetLayer<T>(out var layer) ? layer : throw new InvalidOperationException($"No layer of type {typeof(T).Name}.");

    /// <summary>
    /// Tries to resolve the layer named <paramref name="name"/> as a
    /// <typeparamref name="T"/>. Returns <c>false</c> if no layer has that
    /// name. Throws if the name is duplicated or the named layer is a
    /// different type.
    /// </summary>
    public bool TryGetLayer<T>(string name, [NotNullWhen(true)] out T? layer) where T : class
    {
        ArgumentNullException.ThrowIfNull(name);
        Layer2D? named = null;
        foreach (var candidate in Layers)
        {
            if (candidate.Name != name)
                continue;
            if (named is not null)
                throw new InvalidOperationException($"More than one layer is named '{name}'.");
            named = candidate;
        }
        if (named is null)
        {
            layer = null;
            return false;
        }
        if (named is T typed)
        {
            layer = typed;
            return true;
        }
        throw new InvalidOperationException($"Layer '{name}' is a {named.GetType().Name}, not a {typeof(T).Name}.");
    }

    /// <summary>
    /// Resolves the layer named <paramref name="name"/> as a
    /// <typeparamref name="T"/>. Throws if no such layer exists or it is a
    /// different type.
    /// </summary>
    public T GetLayer<T>(string name) where T : class =>
        TryGetLayer<T>(name, out var layer) ? layer : throw new InvalidOperationException($"No layer named '{name}'.");

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