namespace Raele.GodotReactivity;

public abstract class Observable<T> : Observable
{
	public abstract T ReadUntracked();

	public T Read(EffectContext context)
	{
		return context.Read(this);
	}
}
