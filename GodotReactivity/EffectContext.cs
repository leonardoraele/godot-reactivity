using System.Collections.Generic;

namespace Raele.GodotReactivity;

public class EffectContext : Observable
{
	private HashSet<Observable> Dependencies = new();

    public T Read<T>(Observable<T> observable)
	{
		this.Dependencies.Add(observable);
		observable.Changed += this.NotifyChanged;
		return observable.ReadUntracked();
	}

	public override void Dispose()
	{
		base.Dispose();
		foreach (Observable dependency in this.Dependencies)
		{
			dependency.Changed -= this.NotifyChanged;
		}
	}
}
