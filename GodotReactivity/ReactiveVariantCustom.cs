using System;
using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariantCustom<T> : ReactiveVariant
{
	private T _value;
    private Converter<T, Variant> ToVariant;
    private Converter<Variant, T> FromVariant;

    public ReactiveVariantCustom(T initialValue, Converter<T, Variant> toVariant, Converter<Variant, T> fromVariant)
	{
		this._value = initialValue;
		this.ToVariant = toVariant;
		this.FromVariant = fromVariant;
	}

	public static implicit operator T(ReactiveVariantCustom<T> reactive) => reactive.Value;

	public override Variant VariantValue {
		get => this.ToVariant(this._value);
		set => this._value = this.FromVariant(value);
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
			this._value = value;
			this.NotifyChanged();
		}
	}
}
