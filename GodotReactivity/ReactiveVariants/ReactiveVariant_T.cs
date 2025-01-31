using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariant<[MustBeVariant] T> : AbstractReactiveVariant
{
	protected T _value;

	public ReactiveVariant(T initialValue) => this._value = initialValue;

	public override Variant VariantValue {
		get => Variant.From(this.Value);
		set => this.Value = value.As<T>();
	}

	// It is ok to have implicit read convertion (from ReactiveVariant to Variant), but not the other way around,
	// because an implicit write convertion (from Variant to ReactiveVariant) could lead to accidental recreation of the
	// ReactiveVariant object when updating the .Value property was intended.
	public static implicit operator T(ReactiveVariant<T> reactiveVariant) => reactiveVariant.Value;
	public static implicit operator Variant(ReactiveVariant<T> reactiveVariant) => Variant.From(reactiveVariant.Value);

    public T Value {
		get {
			this.NotifyUsed();
			return this._value;
		}
		set {
			if (value == null ? this._value == null : value.Equals(this._value)) {
				return;
			}
			this._value = value;
			this.NotifyChanged();
		}
	}

	public override string ToString() => $"{this.Value}";
}
