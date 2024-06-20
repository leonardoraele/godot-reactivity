using System;
using Godot;

namespace Raele.GodotReactivity;

public class ComputedState<T> : Observable<T>
{
	private EffectContext? _context { get; set; }
	private Func<EffectContext, T> _computationFunc;
	private T _valueCache;

	public override T ReadUntracked() =>
		this._context?.Dirty == true
			? this.ComputeValue()
			: this._valueCache;

	public ComputedState(Func<EffectContext, T> func)
	{
		this._computationFunc = func;
		this._valueCache = this.ComputeValue();
	}

	public static ComputedState<U> CreateInContext<U>(Node bind, Func<EffectContext, U> func)
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
		return this._valueCache = this._computationFunc(this._context);
	}

	public override void Dispose()
	{
		base.Dispose();
		this._context?.Dispose();
		this._context = null;
	}
}
