using Godot;

namespace Raele.GodotReactivity;

public interface IVariantConverter<T> where T : IVariantConverter<T>
{
	public abstract static Variant ToVariant(T subject);
	public abstract static T FromVariant(Variant variant);

	public virtual static implicit operator Variant(T subject) => T.ToVariant(subject);
	public virtual static implicit operator T(Variant variant) => T.FromVariant(variant);
}
