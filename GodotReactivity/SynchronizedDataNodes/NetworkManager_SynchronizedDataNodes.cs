using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------


	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------


	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------


	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------

	// [Signal] public delegate

	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------

	// public enum

	// -----------------------------------------------------------------------------------------------------------------
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	public void BroadcastState(SynchronizedNode node)
		=> this.Rpc(MethodName.RpcPutState, node.GetPath(), node.Value, this.GetTree().CurrentScene.Name);

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void RpcPutState(NodePath path, Variant value, string currentScene)
	{
		if (currentScene != this.GetTree().CurrentScene.Name) {
			GD.PushWarning($"[{nameof(NetworkManager)}] Received synchronized state for scene {currentScene} but current scene is {this.GetTree().CurrentScene.Name}. Ignoring...");
			return;
		}
		if (this.GetNodeOrNull(path) is SynchronizedState state) {
			state.Value = value;
		} else if (this.GetNodeOrNull(path.GetParentPath()) is Node parent) {
			SynchronizedNode node = SynchronizedNode.From(value);
			node.Name = path.GetName(path.GetNameCount() - 1);
			parent.AddChild(node);
		} else {
			GD.PushError($"[{nameof(NetworkManager)}] Failed to create received synchronized state at path {path}. Value: {value}");
		}
	}
}
