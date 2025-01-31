using Godot;

namespace Raele.GodotReactivity;

public interface IVariantSerializer<T> where T : IVariantSerializer<T>
{
	public abstract static Variant Serialize(T subject);
	public abstract static T Deserialize(Variant variant);

	public virtual static implicit operator Variant(T subject) => T.Serialize(subject);
	public virtual static implicit operator T(Variant variant) => T.Deserialize(variant);
}
