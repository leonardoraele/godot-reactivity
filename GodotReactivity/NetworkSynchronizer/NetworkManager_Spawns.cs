using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

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

	private Dictionary<string, SpawnedNodeRecord> SpawnedNodes = new();

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

	private class SpawnedNodeRecord
	{
		public required string SceneFilePath { get; init; }
        public required Node Node { get; init; }
        public Guid NetId { get; private set; }
        public string NetIdStr {
			get => this.NetId.ToString();
			set => this.NetId = new Guid(value);
		}
		public byte[] NetIdBytes {
			get => this.NetId.ToByteArray();
			set => this.NetId = new Guid(value);
		}
		public Godot.Collections.Array Args { get; init; } = new();
		public NetworkSynchronizer? Synchronizer;
        public SpawnedNodeRecord? AncestorSpawnNetId;

        public List<string> DescendantSpawnNetIds { get; private set; } = new();
    }

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

	private void SetupSpawns()
		=> NetworkManager.Scenes.PeerChangedScene += this.OnPeerChangedScene;

	public void Spawn(PackedScene scene, NodePath parentPath, params Variant[] args)
		=> this.Spawn(scene, this.GetNode(parentPath), args);

	public void Spawn(PackedScene scene, Node parent, params Variant[] args)
		=> this.Spawn(scene.ResourcePath, parent, args);

	public void Spawn(string scenePath, NodePath parentPath, params Variant[] args)
		=> this.Spawn(scenePath, this.GetNode(parentPath), args);

	public void Spawn(string scenePath, Node parent, params Variant[] args)
	{
		if (!parent.IsMultiplayerAuthority()) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn network node. Cause: Local peer is not the multiplayer authority of the parent node.", new { scenePath, ParentPath = parent.GetPath() });
			return;
		}
		byte[] netIdBytes = Guid.NewGuid().ToByteArray();
		// TODO I think we should also send NetworkManager.Connectivity.LocalPeer.CurrentScene.Value.GetHashCode() in
		// this Rpc as a type of checksum so that the receiver peer can be sure that the intention was to spawn in the
		// correct scene. -- What kind of issues would this prevent, exactly?
		this.Rpc(MethodName.RpcSpawn, scenePath, parent.GetPath(), netIdBytes, new Godot.Collections.Array(args));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcSpawn(string sceneFilePath, NodePath parentPath, Variant netIdBytes, Godot.Collections.Array args)
	{
		PackedScene scene = ResourceLoader.Load<PackedScene?>(sceneFilePath)
			?? throw new Exception("Failed to spawn scene for requested RpcSpawn call. path = " + sceneFilePath);
		if (this.Multiplayer.GetRemoteSenderId() != this.Multiplayer.GetUniqueId()) {
			if (!this.ConnectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)) {
				GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn network node. Cause: Unknown sender peer.", new { sceneFilePath, RpcSenderId = this.Multiplayer.GetRemoteSenderId() });
				return;
			}
			if (peer.CurrentScene.Value != NetworkManager.Connectivity.LocalPeer.CurrentScene.Value) {
				GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Ignoring spawn node rpc call. Cause: Different scene.", new { /*uid,*/ RpcSenderId = this.Multiplayer.GetRemoteSenderId()/*, RpcSenderScene = peer?.CurrentScene.Value, LocalPeerScene = this.LocalPeer?.CurrentScene.Value*/ });
				return;
			}
		}
		if (this.GetNodeOrNull(parentPath) is not Node parent) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn network node. Cause: Parent node not found.", new { sceneFilePath, parentPath });
			return;
		}
		if (parent.GetMultiplayerAuthority() != this.Multiplayer.GetRemoteSenderId()) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn network node. Cause: Rpc sender is not multiplayer authority of spawn parent node.", new { sceneFilePath, parentPath, AuthorityId = parent.GetMultiplayerAuthority(), RpcSenderId = this.Multiplayer.GetRemoteSenderId() });
			throw new Exception($"{nameof(NetworkManager)} failed to spawn {scene} at {parentPath}. Only multiplayer authority can spawn nodes.");
		}
        Node instance = scene.Instantiate();
		instance.Name = new Guid(netIdBytes.AsByteArray()).ToString();
		instance.AddToGroup(NetworkManager.SPAWNED_GROUP);
		this.RegisterSpawnedNode(instance, sceneFilePath, args);
		try {
			using (this.SpawnedNodes[instance.Name].Synchronizer?.OfflineMode()) {
				if (instance.HasMethod("_before_enter_tree")) { // TODO Use StringName
					instance.Call("_before_enter_tree", [..args]);
				} else if (instance.HasMethod("_BeforeEnterTree")) {
					instance.Call("_BeforeEnterTree", [..args]);
				}
			}
		} catch (Exception e) {
			this.UnregisterSpawnedNode(this.SpawnedNodes[instance.Name]);
			GD.PushError(
				NetworkManager.NetId,
				nameof(NetworkManager),
				"Failed to network-spawn node.",
				"Cause: Node threw an exception on _before_enter_tree or _BeforeEnterTree before entering tree.",
				"SceneFilePath: ", sceneFilePath,
				"ParentPath: ", parentPath,
				"Args: ", args,
				"Exception: ", e
			);
			return;
		}
		parent.AddChild(instance);
		if (!instance.IsMultiplayerAuthority()) {
			// this.SpawnedNodes[instance.Name].Synchronizer?.Update();
			this.RpcId(instance.GetMultiplayerAuthority(), MethodName.RpcSpawnDescendants, netIdBytes);
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "🎉 Network-spawned scene", sceneFilePath, "as", instance.Name);
	}

	private void RegisterSpawnedNode(Node node, string sceneFilePath, Godot.Collections.Array args)
	{
        SpawnedNodeRecord record = this.SpawnedNodes[node.Name] = new() {
			Node = node,
			SceneFilePath = sceneFilePath,
			NetIdStr = node.Name,
			Args = args,
		};
		if (
			node.GetAncestors().FirstOrDefault(ancestor => ancestor.IsInGroup(SPAWNED_GROUP)) is Node ancestor
			&& this.SpawnedNodes.TryGetValue(ancestor.Name, out SpawnedNodeRecord? ancestorRecord)
		) {
			record.AncestorSpawnNetId = ancestorRecord;
			ancestorRecord.DescendantSpawnNetIds.Add(node.Name);
		}
	}

	private void UnregisterSpawnedNode(SpawnedNodeRecord record)
	{
		this.SpawnedNodes.Remove(record.Node.Name);
		record.DescendantSpawnNetIds.Select(netIdStr => this.SpawnedNodes[netIdStr])
			.ForEach(this.UnregisterSpawnedNode);
    }

	public void RegisterSynchronizer(NetworkSynchronizer synchronizer)
	{
		Node? parent = synchronizer.GetParent();
		if (parent == null) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to register network synchronizer. Cause: Synchronizer not in the tree. (parent is null)");
			return;
		}
		this.RegisterSynchronizer(synchronizer, parent);
	}

	public void RegisterSynchronizer(NetworkSynchronizer synchronizer, Node parent)
	{
		if (!parent.IsInGroup(SPAWNED_GROUP)) {
			// Not a spawned node; no need to register
			return;
		}
		if (!this.SpawnedNodes.TryGetValue(parent.Name, out SpawnedNodeRecord? record)) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to register network synchronizer. Cause: Unknown parent.", new { SynchronizerPath = synchronizer.GetPath() });
			return;
		}
		record.Synchronizer = synchronizer;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcSpawnDescendants(Variant netIdBytes)
	{
		string netIdStr = new Guid(netIdBytes.AsByteArray()).ToString();
		if (!this.SpawnedNodes.TryGetValue(netIdStr, out SpawnedNodeRecord? record)) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to spawn descendants of spawned node. Cause: Unknown network node. NetId:", netIdStr);
			return;
		}
		record.DescendantSpawnNetIds.Select(netId => this.SpawnedNodes[netId])
			.ForEach(descendantRecord => this.RpcId(
				this.Multiplayer.GetRemoteSenderId(),
				MethodName.RpcSpawn,
				descendantRecord.SceneFilePath,
				descendantRecord.Node.GetParent().GetPath(),
				descendantRecord.NetIdBytes,
				descendantRecord.Args
			));
	}

	private void OnPeerChangedScene(ConnectedPeer peer, string previousScene)
	{
		if (peer == this.LocalPeer) {
			this.SpawnedNodes.Clear();
		} else if (peer.CurrentScene.Value == NetworkManager.Connectivity.LocalPeer.CurrentScene.Value) {
            IEnumerable<SpawnedNodeRecord> topLevelSpawnRecords = this.SpawnedNodes.Values
				.Where(record => record.Node.IsMultiplayerAuthority())
				.Where(record => record.AncestorSpawnNetId == null);
			GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), $"Detected new peer in current scene. Spawning {topLevelSpawnRecords.Count()} nodes...", new { Scene = this.LocalPeer?.CurrentScene.Value ?? "null", PeerId = peer.Id });
			topLevelSpawnRecords.ForEach(record =>
				this.RpcId(
					peer.Id,
					MethodName.RpcSpawn,
					record.SceneFilePath,
					record.Node.GetParent().GetPath(),
					record.NetIdBytes,
					record.Args
				)
			);
		}
	}

	public void Despawn(NodePath nodePath) => this.Despawn(this.GetNode(nodePath));
	public void Despawn(Node node)
	{
		if (!node.IsMultiplayerAuthority()) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to despawn network node. Cause: Local peer is not the multiplayer authority of the node.", new { NodePath = node.GetPath(), AuthorityId = node.GetMultiplayerAuthority(), LocalPeerId = this.Multiplayer.GetUniqueId() });
			return;
		}
		this.Rpc(MethodName.RpcDespawn, new Guid(node.Name).ToByteArray());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcDespawn(Variant netIdBytes)
	{
		string netIdStr = new Guid(netIdBytes.AsByteArray()).ToString();
		if (!this.SpawnedNodes.TryGetValue(netIdStr, out SpawnedNodeRecord? record)) {
			GD.PushWarning(NetworkManager.NetId, nameof(NetworkManager), "Failed to despawn network node. Cause: Unknown network node. NetId:", netIdStr);
			return;
		}
		if (this.Multiplayer.GetRemoteSenderId() != record.Node.GetMultiplayerAuthority()) {
			GD.PushError(NetworkManager.NetId, nameof(NetworkManager), "Failed to despawn network node. Cause: Rpc sender is not multiplayer authority of despawning node.", new { netIdStr, RpcSenderId = this.Multiplayer.GetRemoteSenderId(), AuthorityId = record.Node.GetMultiplayerAuthority(), LocalPeerId = this.Multiplayer.GetUniqueId() });
			return;
		}
		record.Node.QueueFree();
		this.UnregisterSpawnedNode(record);
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "❌ Despawned a network node.", new { Path = record.Node.GetPath() });
	}

	public bool CheckIsNetworkSpawned(Node node) => node.IsInGroup(SPAWNED_GROUP);
}
