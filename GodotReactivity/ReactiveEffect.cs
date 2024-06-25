using System;
using Godot;

namespace Raele.GodotReactivity;

public partial class ReactiveEffect : IDisposable
{
    private Action<EffectContext> _effectAction;
    private EffectContext? _context;
	private Callable _runCallable;

    public ReactiveEffect(Action<EffectContext> action)
	{
		this._effectAction = action;
		this._runCallable = Callable.From(this.Run);
		this.Run();
	}

	public static ReactiveEffect CreateInContext(Node node, Action<EffectContext> action)
	{
		ReactiveEffect effect = new(action);
		node.TreeExiting += effect.Dispose;
		return effect;
	}

	public void ForceRerun() => this._context?.NotifyChanged();

    private void Run()
	{
		this._context?.Dispose();
		this._context = new();
		this._context.Changed += () => this._runCallable.CallDeferred();
		this._effectAction(this._context);
	}

	public void Dispose()
	{
		this._context?.Dispose();
		this._context = null;
	}
}
