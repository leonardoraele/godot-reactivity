using Godot;

namespace Raele.GodotReactivity;

public partial class ConnectedPeer : GodotObject
{
	public required long Id { get; init; }
	public ReactiveState<NodePath?> CurrentScene = new(null);
	public bool IsLocalPeer => NetworkManager.Instance.LocalPeer == this;

	public string DisplayName => $"Player #{this.Id}";

	public bool IsInSameScene(ConnectedPeer other)
	{
		NodePath? currentScene = this.CurrentScene.Value;
		return currentScene != null
			&& currentScene == other.CurrentScene.Value;
	}
}
