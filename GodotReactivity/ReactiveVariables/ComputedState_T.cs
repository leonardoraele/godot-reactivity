using System;
using Godot;

namespace Raele.GodotReactivity;

public class ComputedState<T> : Observable<T>
{
	private EffectContext? _context { get; set; }
	private Func<T> _computationFunc;
	private T _valueCache;

	public override T Value {
		get {
			this.NotifyUsed();
			return this._context?.Dirty == true
				? this.ComputeValue()
				: this._valueCache;
		}
		set => GD.PushWarning("Tried to set a value on a ComputedState, which is read-only. Assigned will be ignored.");
	}

	public static implicit operator T(ComputedState<T> computedState) => computedState.Value;

	public ComputedState(Func<T> func)
	{
		this._computationFunc = func;
		this._valueCache = this.ComputeValue();
	}

	public static ComputedState<U> CreateInContext<U>(Node bind, Func<U> func)
	{
		ComputedState<U> state = new(func);
		bind.TreeExiting += state.Dispose;
		return state;
	}

	private T ComputeValue()
	{
		this._context?.Dispose();
		this._context = new();
		this._context.Changed += this.NotifyChanged;
		this._context.Run(() => this._valueCache = this._computationFunc());
		return this._valueCache;
	}

	public override void Dispose()
	{
		base.Dispose();
		this._context?.Dispose();
		this._context = null;
	}
}
