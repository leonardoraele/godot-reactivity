using Godot;

namespace Raele.GodotReactivity;

public interface IVariantConverter<T>
{
	public abstract static Variant ToVariant(T subject);
	public abstract static T FromVariant(Variant variant);
}
