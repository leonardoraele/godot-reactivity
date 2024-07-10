using System;

namespace Raele.GodotReactivity;

public abstract class Observable : IDisposable
{
	public bool Dirty { get; private set; } = false;

	public event Action? Changed;

	public void NotifyUsed() {
		if (EffectContext.TryGetContext(out EffectContext? context)) {
			context.AddDependency(this);
		}
	}

    public void NotifyChanged() {
		this.Dirty = true;
		this.Changed?.Invoke();
	}

	public virtual void Dispose()
	{
		this.Changed = null;
	}
}
