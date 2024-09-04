using System;
using System.Reactive.Disposables;
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

    public static ReactiveEffect CreateWithToken(CancellationToken token, Action action)
    {
		ReactiveEffect effect = new(action);
        token.Register(effect.Dispose);
		return effect;
    }

	/// <summary>
	/// Rerun the effect even if the context is not dirty. (i.e. no dependant state has changed)
	/// </summary>
	public void ForceRerun(bool deferred = true)
	{
		this._context?.Dispose();
		this._context = null;
		this.SafeRerun(deferred);
	}

	/// <summary>
	/// Rerun the effect only if the context is dirty. (i.e. some dependant state has changed) Calling this is necessary
	/// to rerun the effect manually if it's disabled.
	/// </summary>
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
		if (this._context?.Dirty == false || !this.Enabled) {
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
		if (this._context.Empty) {
			this.Dispose();
		}
	}

	public void Dispose()
	{
		this._context?.Dispose();
		this._context = null;
		this.Enabled = false;
	}

	public IDisposable DisabledContext()
	{
		this.Enabled = false;
		return Disposable.Create(() => this.Enabled = true);
	}
}
