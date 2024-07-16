using System;

namespace Raele.GodotReactivity;

public abstract class Observable : IDisposable
{
	public event Action? Changed;

	public void NotifyUsed()
	{
		if (EffectContext.TryGetContext(out EffectContext? context)) {
			context.AddDependency(this);
		}
	}

    public virtual void NotifyChanged()
	{
		this.Changed?.Invoke();
	}

	public virtual void Dispose()
	{
		this.Changed = null;
	}
}
