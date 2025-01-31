using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariantCompatible<T> : AbstractReactiveVariant where T : IVariantSerializer<T>
{
	protected T _value;

    public ReactiveVariantCompatible(T initialValue) => this._value = initialValue;

	// It is ok to have implicit read convertion (from ReactiveVariant to Variant), but not the other way around,
	// because an implicit write convertion (from Variant to ReactiveVariant) could lead to accidental recreation of the
	// ReactiveVariant object when updating the .Value property was intended.
	public static implicit operator T(ReactiveVariantCompatible<T> reactive) => reactive.Value;
	public static implicit operator Variant(ReactiveVariantCompatible<T> reactive) => T.Serialize(reactive.Value);

	public override Variant VariantValue {
		get => T.Serialize(this.Value);
		set => this.Value = T.Deserialize(value);
	}

    public T Value {
		get {
			this.NotifyUsed();
			return this._value;
		}
		set {
			if (value == null ? this._value == null : value.Equals(this._value)) {
				return;
			}
			this._value = value!;
			this.NotifyChanged();
		}
	}
}
