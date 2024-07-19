using Godot;

namespace Raele.GodotReactivity;

public abstract class ReactiveVariant : Observable
{
	public abstract Variant VariantValue { get; set; }

	public static implicit operator Variant(ReactiveVariant reactiveVariant)
		=> reactiveVariant.VariantValue;
}
