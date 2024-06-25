using Godot;

namespace Raele.GodotReactivity;

public class VariantState : Observable<Variant>
{
	private Variant _state;

    public VariantState(Variant initialValue) {
		this._state = initialValue;
	}

    public override Variant ReadUntracked() => this._state;

	public void Write(Variant value)
	{
		if (value.Equals(this._state) == false) {
			this._state = value;
			this.NotifyChanged();
		}
	}
}
