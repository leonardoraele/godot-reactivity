using Godot;

namespace Raele.GodotReactivity;

public abstract class AbstractReactiveVariant : Observable
{
	public abstract Variant VariantValue { get; set; }

	public static implicit operator Variant(AbstractReactiveVariant reactiveVariant)
		=> reactiveVariant.VariantValue;

    public override string ToString() => this.VariantValue.ToString();
}
