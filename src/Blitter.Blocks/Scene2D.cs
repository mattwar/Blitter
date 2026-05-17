using System.Collections.Immutable;

namespace Blitter.Blocks;

/// <summary>
/// A composition of stacked <see cref="Layer2D"/>s rendered
/// back-to-front each tick. Drives the per-frame loop against a
/// <see cref="Window2D"/> via <see cref="RunAsync"/>.
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
        _layers = layers.ToImmutableList();
    }

    public Scene2D(params Layer2D[] layers)
    {
        _layers = layers.ToImmutableList();
    }

    /// <summary>The layers in this scene, back-to-front.</summary>
    public ImmutableList<Layer2D> Layers => _layers;

    public void AddLayer(Layer2D layer)
        => ImmutableInterlocked.Update(ref _layers, (list, l) => list.Add(l), layer);

    /// <summary>
    /// Inserts <paramref name="layer"/> at <paramref name="index"/>.
    /// Lower indexes render first (further back).
    /// </summary>
    public void InsertLayer(int index, Layer2D layer)
        => ImmutableInterlocked.Update(ref _layers, (list, args) => list.Insert(args.index, args.layer), (index, layer));

    public void RemoveLayer(Layer2D layer)
        => ImmutableInterlocked.Update(ref _layers, (list, l) => list.Remove(l), layer);

    public void Update(in UpdateContext2D context)
    {
        var layers = _layers;
        foreach (var layer in layers)
        {
            if (layer.Enabled)
                layer.Update(in context);
        }
    }

    public void Draw(Renderer2D renderer)
    {
        var layers = _layers;
        foreach (var layer in layers)
        {
            if (layer.Visible)
                layer.Draw(renderer);
        }
    }

    /// <summary>
    /// Runs the scene on a dedicated render thread until canceled or
    /// another exit condition is reached. The returned task completes
    /// when the loop exits, so multiple scenes / windows can be
    /// composed via <see cref="Task.WhenAll(Task[])"/>.
    /// </summary>
    public async Task RunAsync(Window2D window, CancellationToken cancellationToken = default)
    {
        _window = window;

        try
        {
            await window.RunAsync(
                shouldContinue: () => !ShouldExit(),
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

    protected virtual bool ShouldExit()
    {
        return _window == null
            || _window.IsClosed;
    }
}
