using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Raele.GodotReactivity;

public class EffectContext : Observable
{
	private static Dictionary<Thread, Stack<EffectContext>> ContextByThread = new();

	public static EffectContext? GetContext()
	{
		return ContextByThread.TryGetValue(Thread.CurrentThread, out Stack<EffectContext>? stack)
			&& stack.TryPeek(out EffectContext? context)
				? context
				: null;
	}

	public static bool TryGetContext([NotNullWhen(true)] out EffectContext? context)
	{
		context = EffectContext.GetContext();
		return context != null;
	}

	public bool Dirty { get; private set; } = false;
	private HashSet<Observable> Dependencies = new();

	public bool Empty => this.Dependencies.Count == 0;

	public override void NotifyChanged()
	{
		base.NotifyChanged();
		this.Dirty = true;
	}

	public void AddDependency(Observable observable)
	{
		if (!this.Dependencies.Contains(observable)) {
			this.Dependencies.Add(observable);
			observable.Changed += this.NotifyChanged;
		}
	}

	public void Run(Action action)
	{
		Stack<EffectContext> stack = EffectContext.ContextByThread.GetValueOrDefault(Thread.CurrentThread) ?? new();
		EffectContext.ContextByThread.TryAdd(Thread.CurrentThread, stack);
		stack.Push(this);
		try {
			action();
		} catch {
			stack.Pop();
			if (stack.Count == 0) {
				EffectContext.ContextByThread.Remove(Thread.CurrentThread);
			}
			throw;
		}
	}

	public override void Dispose()
	{
		base.Dispose();
		foreach (Observable dependency in this.Dependencies) {
			dependency.Changed -= this.NotifyChanged;
		}
	}
}
