using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariant : AbstractReactiveVariant
{
	protected Variant _value;

	public ReactiveVariant() => this._value = new Variant();
	public ReactiveVariant(Variant value) => this._value = value;

	// It is ok to have implicit read convertion (from ReactiveVariant to Variant), but not the other way around,
	// because an implicit write convertion (from Variant to ReactiveVariant) could lead to accidental recreation of the
	// ReactiveVariant object when updating the .Value property was intended.
	public static implicit operator Variant(ReactiveVariant reactive) => reactive.Value;

	public override Variant VariantValue {
		get => this.Value;
		set => this.Value = value;
	}

    public Variant Value {
		get {
			this.NotifyUsed();
			return this._value;
		}
		set {
			if (this._value.Equals(value)) {
				return;
			}
			this._value = value;
			this.NotifyChanged();
		}
	}
}
