using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariant : Observable<Variant>
{
	private Variant _state;

	public ReactiveVariant() {}
    public ReactiveVariant(Variant initialValue) => this._state = initialValue;

	public override Variant Value {
		get {
			this.NotifyUsed();
			return this._state;
		}
		set {
			if (!value.Equals(this._state)) {
				this._state = value;
				this.NotifyChanged();
			}
		}
	}
}
