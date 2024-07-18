using Godot;

namespace Raele.GodotReactivity;

public abstract class ReactiveVariant : Observable
{
	public abstract Variant VariantValue { get; set; }
}
