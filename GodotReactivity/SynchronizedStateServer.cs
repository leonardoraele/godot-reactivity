using Godot;
using Raele.PocketWars;

namespace Raele.GodotReactivity;

public partial class SynchronizedStateServer : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public static SynchronizedStateServer Instance { get; private set; } = null!;

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

	// [Signal] public delegate void EventHandler()

	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------

	// private enum Type {
	// 	Value1,
	// }

	// -----------------------------------------------------------------------------------------------------------------
	// EVENTS
	// -----------------------------------------------------------------------------------------------------------------

	public override void _EnterTree()
	{
		base._EnterTree();
		if (SynchronizedStateServer.Instance != null) {
			GD.PushError($"Failed to set {nameof(SynchronizedStateServer)}.{nameof(SynchronizedStateServer.Instance)} because it is already set.");
			this.QueueFree();
			return;
		}
		SynchronizedStateServer.Instance = this;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		SynchronizedStateServer.Instance = null!;
	}

	// public override void _Ready()
	// {
	// 	base._Ready();
	// }

	// public override void _Process(double delta)
	// {
	// 	base._Process(delta);
	// }

	// public override void _PhysicsProcess(double delta)
	// {
	// 	base._PhysicsProcess(delta);
	// }

	// public override string[] _GetConfigurationWarnings()
	// 	=> base._PhysicsProcess(delta);

	// -----------------------------------------------------------------------------------------------------------------
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	public void BroadcastState(SynchronizedNode node) => this.Rpc(MethodName.RpcPutState, node.GetPath(), node.Value, this.GetTree().CurrentScene.Name);

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void RpcPutState(NodePath path, Variant value, string currentScene)
	{
		if (currentScene != this.GetTree().CurrentScene.Name) {
			GD.PushWarning($"[{nameof(SynchronizedStateServer)}] Received synchronized state for scene {currentScene} but current scene is {this.GetTree().CurrentScene.Name}. Ignoring...");
			return;
		}
		if (this.GetNodeOrNull(path) is SynchronizedState state) {
			state.Value = value;
		} else if (this.GetNodeOrNull(path.GetParentPath()) is Node parent) {
			SynchronizedNode node = SynchronizedNode.From(value);
			node.Name = path.GetName(path.GetNameCount() - 1);
			parent.AddChild(node);
		} else {
			GD.PushError($"[{nameof(SynchronizedStateServer)}] Failed to create received synchronized state at path {path}. Value: {value}");
		}
	}
}
