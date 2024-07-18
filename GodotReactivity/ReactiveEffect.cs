using System;
using System.Threading;
using Godot;

namespace Raele.GodotReactivity;

public class ReactiveEffect : IDisposable
{
    private Action _effectAction;
    private EffectContext? _context;
	private Callable _runCallable;

	public bool Enabled = true;

	public CancellationToken? Token {
		init => value?.Register(this.Dispose);
	}

    public ReactiveEffect(Action action)
	{
		this._effectAction = action;
		this._runCallable = Callable.From(this.Run);
		this.Run();
	}

	public static ReactiveEffect CreateInContext(Node node, Action action)
	{
		ReactiveEffect effect = new(action);
		node.TreeExiting += effect.Dispose;
		return effect;
	}

	public void ForceRerun(bool deferred = true)
	{
		this._context?.Dispose();
		this._context = null;
		this.SafeRerun(deferred);
	}

	public void SafeRerun(bool deferred = true)
	{
		if (deferred) {
			this.RunDeferred();
		} else {
			this.Run();
		}
	}

	private void RunDeferred() => this._runCallable.CallDeferred();

    private void Run()
	{
		if (this._context?.Dirty == false) {
			return;
		}
		this._context?.Dispose();
        this._context = new();
        this._context.Changed += () => {
			if (this.Enabled) {
				this.RunDeferred();
			}
		};
		this._context.Run(this._effectAction);
	}

	public void Dispose()
	{
		this._context?.Dispose();
		this._context = null;
	}

	public IDisposable DisabledContext()
	{
		this.Enabled = false;
		return new EffectDisableContext(() => this.Enabled = true);
	}

	public class EffectDisableContext : IDisposable
	{
        private Action OnDisposed;
		public EffectDisableContext(Action onDisposed) => this.OnDisposed = onDisposed;
		public void Dispose() => this.OnDisposed();
	}
}
