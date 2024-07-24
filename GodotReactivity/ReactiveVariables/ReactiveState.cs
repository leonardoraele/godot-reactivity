using System.Collections.Generic;

namespace Raele.GodotReactivity;

public class ReactiveState<T> : Observable<T>
{
	private T _state;
    private EqualityComparer<T> _equalityComparer;

	public static implicit operator T(ReactiveState<T> reactiveState) => reactiveState.Value;

	public override T Value {
		get {
			this.NotifyUsed();
			return this._state;
		}
		set {
			if (this._equalityComparer.Equals(this._state, value) == false) {
				this._state = value;
				this.NotifyChanged();
			}
		}
	}

    public ReactiveState(T initialValue, EqualityComparer<T>? equalityComparer = null) {
		this._state = initialValue;
		this._equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
	}

	public override string ToString() => $"{this.Value}";
}
