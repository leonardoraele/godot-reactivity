using System;
using Godot;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public const string SPAWNED_GROUP = "__network_spawned";

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	// private ReactiveDictionary<Guid, INetworkSpawnable> _networkSpawnedNodesById = new();

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	// public IReadOnlyDictionary<Guid, INetworkSpawnable> NetworkNodesById => this._networkSpawnedNodesById;

	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------


	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------


	// -----------------------------------------------------------------------------------------------------------------
	// EVENTS
	// -----------------------------------------------------------------------------------------------------------------

	// public override void _EnterTree()
	// {
	// 	base._EnterTree();
	// }

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
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	public void Spawn(PackedScene scene, NodePath parentPath, params Variant[] args)
		=> this.Spawn(scene, this.GetNode(parentPath), args);

	public void Spawn(PackedScene scene, Node parent, params Variant[] args)
	{
		if (!parent.IsMultiplayerAuthority()) {
			throw new Exception($"{nameof(NetworkManager)} failed to spawn {scene} at node {parent.GetPath()}. Local peer is not the multiplayer authority of this node.");
		}
		Guid netId = Guid.NewGuid();
		this.Rpc(
			MethodName.RpcSpawn,
			ResourceUid.IdToText(ResourceLoader.GetResourceUid(scene.ResourcePath)),
			parent.GetPath(),
			netId.ToByteArray(),
			new Godot.Collections.Array(args)
		);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private async void RpcSpawn(string uid, NodePath parentPath, Variant netId, Godot.Collections.Array args)
	{
		PackedScene scene = ResourceLoader.Load<PackedScene?>(ResourceUid.GetIdPath(ResourceUid.TextToId(uid)))
			?? throw new Exception("Failed to spawn scene for requested RpcSpawn call. uid = " + uid + " / HasId:" + ResourceUid.HasId(ResourceUid.TextToId(uid)));
		if (this.Multiplayer.GetRemoteSenderId() != this.Multiplayer.GetUniqueId()) {
			if (!this.ConnectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)) {
				GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn network node. Cause: Unknown sender peer.", new { uid, RpcSenderId = this.Multiplayer.GetRemoteSenderId() });
				return;
			}
			if (this.LocalPeer?.IsInSameScene(peer) != true) {
				GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Ignoring spawn node rpc call. Cause: Different scene.", new { uid, RpcSenderId = this.Multiplayer.GetRemoteSenderId(), RpcSenderScene = peer?.CurrentScene.Value, LocalPeerScene = this.LocalPeer?.CurrentScene.Value });
				return;
			}
		}
		if (this.GetNodeOrNull(parentPath) is not Node parent) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn network node. Cause: Parent node not found.", new { uid, parentPath });
			return;
		}
		if (parent.GetMultiplayerAuthority() != this.Multiplayer.GetRemoteSenderId()) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn network node. Cause: Rpc sender is not multiplayer authority of spawn parent node.", new { uid, parentPath, AuthorityId = parent.GetMultiplayerAuthority(), RpcSenderId = this.Multiplayer.GetRemoteSenderId() });
			throw new Exception($"{nameof(NetworkManager)} failed to spawn {scene} at {parentPath}. Only multiplayer authority can spawn nodes.");
		}
        Node instance = scene.Instantiate();
		instance.Name = new Guid(netId.AsByteArray()).ToString();
		instance.AddToGroup(NetworkManager.SPAWNED_GROUP);
		parent.AddChild(instance);
		if (instance.HasMethod("_NetworkSpawned")) { // TODO Use StringName instead
			instance.Call("_NetworkSpawned", [..args]);
		}
		if (!instance.IsMultiplayerAuthority() && instance.GetNodeOrNull(nameof(NetworkSynchronizer)) is NetworkSynchronizer synchronizer) {
#pragma warning disable CS4014 // CS4014: Consider applying the 'await' operator to the result of the call.
			synchronizer.Update(); // Lack of 'await' is intentional here.
#pragma warning restore CS4014
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Spawned new network node.", new { uid, Path = instance.GetPath() });
	}

	public void Despawn(Node node) => this.Despawn(node.GetPath());
	public void Despawn(NodePath nodePath) => this.Rpc(MethodName.RpcDespawn, nodePath);

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcDespawn(NodePath nodePath) // TODO Should use the netId instead of the path to find the node
	{
		Node? node = this.GetNodeOrNull(nodePath);
		if (node == null) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to despawn network node. Cause: Node not found.", new { Path = nodePath });
			return;
		}
		if (!node.IsInGroup(NetworkManager.SPAWNED_GROUP)) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to despawn network node. Cause: Node in path is not a network node. (not in the expected group).", new { Path = nodePath });
			return;
		}
		node.QueueFree();
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Despawned a network node.", new { Path = nodePath });
	}
}
