using System;
using System.Linq;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public static NetworkManager Instance { get; private set; } = null!;
	public static string NetId = string.Join("", Guid.NewGuid().ToString().TakeLast(13));

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
	// EVENTS
	// -----------------------------------------------------------------------------------------------------------------

	public override void _EnterTree()
	{
		base._EnterTree();
		this.SetupInstance();
		this.SetupConnections();
	}

	// public override void _ExitTree()
	// {
	// 	base._ExitTree();
	// }

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
	// SETUP METHODS
	// -----------------------------------------------------------------------------------------------------------------

	private void SetupInstance()
	{
		if (NetworkManager.Instance != null) {
			GD.PushError($"Failed to set {nameof(NetworkManager)}.{nameof(NetworkManager.Instance)} because it is already set.");
			this.QueueFree();
			return;
		}
		NetworkManager.Instance = this;
		this.TreeExiting += () => {
			if (NetworkManager.Instance == this) {
				NetworkManager.Instance = null!;
			}
		};
	}

	// -----------------------------------------------------------------------------------------------------------------
	// SYNCHRONIZED NODE HELPER METHODS
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
