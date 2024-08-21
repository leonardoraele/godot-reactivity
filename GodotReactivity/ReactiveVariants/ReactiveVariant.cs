using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariant : AbstractReactiveVariant
{
	protected Variant _value;

	public ReactiveVariant() => this._value = new Variant();
	public ReactiveVariant(Variant value) => this._value = value;

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

	public static implicit operator Variant(ReactiveVariant reactiveVariant) => reactiveVariant.Value;
}
