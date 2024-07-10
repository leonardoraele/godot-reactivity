using Godot;

namespace Raele.GodotReactivity;

public class VariantState : Observable<Variant>
{
	private Variant _state;

    public VariantState(Variant initialValue) {
		this._state = initialValue;
	}

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
