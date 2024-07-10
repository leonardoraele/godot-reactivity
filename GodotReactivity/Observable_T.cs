namespace Raele.GodotReactivity;

public abstract class Observable<T> : Observable
{
	public abstract T Value { get; set; }
}
