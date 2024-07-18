using Godot;

namespace Raele.GodotReactivity;

public partial class ConnectedPeer : GodotObject
{
	public required long Id { get; init; }
	public ReactiveState<NodePath?> CurrentScene = new(null);

	public string DisplayName => $"Player #{this.Id}";
}
