using Godot;

namespace Raele.GodotReactivity;

public partial class ConnectedPeer : GodotObject
{
	public required long Id { get; init; }
	/// <summary>
	/// This is the file path of the scene that the peer is currently in. This is an empty string in case the peer is
	/// not in a scene (i.e. the current scene node is null) or the current scene node doesn't have a SceneFilePath
	/// field.
	/// </summary>
	public ReactiveState<string> CurrentScene = new("");
	public bool IsLocalPeer => NetworkManager.Instance.LocalPeer == this;

	public string DisplayName => $"Player #{this.Id}";
}
