using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariantCompatible<T> : AbstractReactiveVariant where T : IVariantConverter<T>
{
	protected T _value;

    public ReactiveVariantCompatible(T initialValue) => this._value = initialValue;

	public static implicit operator T(ReactiveVariantCompatible<T> reactive) => reactive.Value;
	public static implicit operator Variant(ReactiveVariantCompatible<T> reactive) => T.ToVariant(reactive.Value);

	public override Variant VariantValue {
		get => T.ToVariant(this.Value);
		set => this.Value = T.FromVariant(value);
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
