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
		context = GetContext();
		return context != null;
	}

	private HashSet<Observable> Dependencies = new();

	public void AddDependency(Observable observable)
	{
		if (!this.Dependencies.Contains(observable)) {
			this.Dependencies.Add(observable);
			observable.Changed += this.NotifyChanged;
		}
	}

	public void Run(Action action)
	{
		Stack<EffectContext> stack = ContextByThread.TryGetValue(Thread.CurrentThread, out Stack<EffectContext>? existingStack)
			? existingStack
			: new();
		ContextByThread.TryAdd(Thread.CurrentThread, stack);
		stack.Push(this);
		try {
			action();
		} finally {
			ContextByThread.Remove(Thread.CurrentThread);
		}
		stack.Pop();
		if (stack.Count == 0) {
			ContextByThread.Remove(Thread.CurrentThread);
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
