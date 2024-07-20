using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public const string SERVER_BIND_ADDRESS = "*";
	public const int DEFAULT_PORT = 3000;

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	public ReactiveState<ConnectionStateEnum> ConnectionState = new(ConnectionStateEnum.Offline);
	private ReactiveDictionary<long, ConnectedPeer> _connectedPeers = new();
	private ReactiveDictionary<Guid, NetworkSpawnableNode> _networkSpawnedNodesById = new();

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	public IReadOnlyDictionary<Guid, NetworkSpawnableNode> NetworkNodesById => this._networkSpawnedNodesById;
	public IReadOnlyDictionary<long, ConnectedPeer> ConnectedPeers => this._connectedPeers;
	public IEnumerable<ConnectedPeer> PeersInScene
	{
		get {
			NodePath currentScene = this.LocalCurrentScene;
			return this._connectedPeers.Values.Where(peer => currentScene == peer.CurrentScene.Value);
		}
	}
	private NodePath LocalCurrentScene => this.GetTree().CurrentScene.GetPath();

	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------

	// Server Connection Signals
	[Signal] public delegate void ServerOpenedEventHandler();
	[Signal] public delegate void ServerClosedEventHandler();

	// Client Connection Signals
	[Signal] public delegate void ConnectedToServerEventHandler();
	[Signal] public delegate void DisconnectedFromServerEventHandler();

	// Server & Client Signals
	[Signal] public delegate void PeerConnectedEventHandler(ConnectedPeer peer);
	[Signal] public delegate void PeerDisconnectedEventHandler(ConnectedPeer peer);
	[Signal] public delegate void PeerSceneChangedEventHandler(ConnectedPeer peer);

	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------

	public enum ConnectionStateEnum {
		Offline,
		Host,
		ClientConnecting,
		ClientConnected,
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

	private void SetupConnections()
	{
		ulong? lastSceneId = this.GetTree().CurrentScene.GetInstanceId();
		this.GetTree().TreeChanged += () => {
			if (
				this.ConnectionState.Value != ConnectionStateEnum.Host
				&& this.ConnectionState.Value != ConnectionStateEnum.ClientConnected
			) {
				return;
			}
			ulong? currentSceneId = this.GetTree().CurrentScene?.GetInstanceId();
			if (currentSceneId != lastSceneId) {
				lastSceneId = currentSceneId;
				this.BroadcastCurrentScene();
			}
		};
	}

    public void OpenMultiplayerServer(int port = DEFAULT_PORT)
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Server started.");
		WebSocketMultiplayerPeer serverPeer = new WebSocketMultiplayerPeer();
		serverPeer.CreateServer(port, SERVER_BIND_ADDRESS);
		this.Multiplayer.MultiplayerPeer = serverPeer;
		this.Multiplayer.PeerConnected += this.OnPeerConnected;
		this.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
		this.ConnectionState.Value = ConnectionStateEnum.Host;
		this.EmitSignal(SignalName.ServerOpened);
	}

	public async Task ConnectToServer(string connectAddress)
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Connecting to server...", new { connectAddress });
		WebSocketMultiplayerPeer clientPeer = new WebSocketMultiplayerPeer();
		clientPeer.CreateClient(connectAddress);
		this.Multiplayer.MultiplayerPeer = clientPeer;
		TaskCompletionSource source = new();
		this.Multiplayer.ConnectedToServer += source.SetResult;
		this.Multiplayer.ConnectionFailed += source.SetCanceled;
		this.Multiplayer.PeerConnected += this.OnPeerConnected;
		this.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
		this.ConnectionState.Value = ConnectionStateEnum.ClientConnecting;
		try {
			await source.Task;
			GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Connected successfully.");
		} catch {
			GD.PrintErr(nameof(NetworkManager), "Failed to connect to server.");
			this.DisconnectFromServer();
			throw;
		} finally {
			this.Multiplayer.ConnectedToServer -= source.SetResult;
			this.Multiplayer.ConnectionFailed -= source.SetCanceled;
		}
		this.Multiplayer.ServerDisconnected += this.DisconnectFromServer;
		this.BroadcastCurrentScene();
		this.ConnectionState.Value = ConnectionStateEnum.ClientConnected;
		this.EmitSignal(SignalName.ConnectedToServer);
	}

    private void OnPeerConnected(long peerId)
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Peer connected.", peerId);
        ConnectedPeer peer = this._connectedPeers[peerId] = new() { Id = peerId };
		this.EmitSignal(SignalName.PeerConnected, peer);
	}

	private void OnPeerDisconnected(long id)
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Peer disconnected.", id);
		this._connectedPeers.Remove(id);
		this.EmitSignal(SignalName.PeerDisconnected, id);
	}

	public void CloseMultiplayerServer()
	{
		if (this.ConnectionState.Value == ConnectionStateEnum.Offline) {
			return;
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Server closed.");
		this.EndConnection();
		this.EmitSignal(SignalName.ServerClosed);
	}

	public void DisconnectFromServer()
	{
		if (this.ConnectionState.Value == ConnectionStateEnum.Offline) {
			return;
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Disconnected from server.");
		this.EndConnection();
		this.Multiplayer.ServerDisconnected -= DisconnectFromServer;
		this.EmitSignal(SignalName.DisconnectedFromServer);
	}

	private void EndConnection()
	{
		this._connectedPeers.Clear();
		this.Multiplayer.MultiplayerPeer = null;
		this.ConnectionState.Value = ConnectionStateEnum.Offline;
		this.Multiplayer.PeerConnected -= this.OnPeerConnected;
		this.Multiplayer.PeerDisconnected -= this.OnPeerDisconnected;
	}

    private void BroadcastCurrentScene()
    {
		NodePath? currentScene = this.GetTree().CurrentScene?.GetPath();
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Broadcasting scene change... To Scene:", currentScene);
		this.Rpc(MethodName.RpcNotifyPeerSceneChanged, currentScene ?? new Variant());
    }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcNotifyPeerSceneChanged(NodePath? scenePath)
	{
		if (!this._connectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)) {
			GD.PushError(
				nameof(NetworkManager),
				"Received peer scene change notification from an invalid peer.",
				new {
					RemoveSenderId = this.Multiplayer.GetRemoteSenderId(),
					ConnectedPeers = this._connectedPeers.Keys
				}
			);
			return;
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Remote peer changed scene.", "Peer Id:", peer.Id, "To Scene:", scenePath);
		peer.CurrentScene.Value = scenePath;
		this.EmitSignal(SignalName.PeerSceneChanged, peer);
	}

	// -----------------------------------------------------------------------------------------------------------------
	// OBJECT SPAWNING & SYNCHRONIZATION METHODS
	// -----------------------------------------------------------------------------------------------------------------

	public void Spawn(PackedScene scene, NodePath parentPath, string? name = null, params Variant[] args)
		=> this.Spawn(scene, this.GetNode(parentPath), name, args);

	public async void Spawn(PackedScene scene, Node parent, string? name = null, params Variant[] args)
	{
		if (!parent.IsMultiplayerAuthority()) {
			throw new Exception($"{nameof(NetworkManager)} failed to spawn {scene} at node {parent.GetPath()}. Local peer is not the multiplayer authority of this node.");
		}
		Guid netId = Guid.NewGuid();
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Spawning new network node...", new { netId, Parent = parent.GetPath() });
		// TODO Waiting for all peers to respond seems a bad idea...
		await this.BiDiRpc(
			this,
			MethodName.RpcSpawn,
			scene,
			parent.GetPath(),
			netId.ToByteArray(),
			name ?? new Variant(),
			new Godot.Collections.Array(args)
		);
		this.Rpc(MethodName.RpcNotifyNetworkReady, netId.ToByteArray(), new Godot.Collections.Array(args));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcSpawn(PackedScene scene, NodePath parentPath, Variant netId, Variant name)
	{
		if (
			!this.ConnectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)
			|| peer.CurrentScene.Value != this.LocalCurrentScene
		) {
			GD.PrintS($"[{nameof(NetworkManager)}] Received spawn request from a peer in a different scene.", new { scene.ResourceName, parentPath, this.LocalCurrentScene });
			return;
		}
		if (this.GetNodeOrNull(parentPath) is not Node parent) {
			throw new Exception($"{nameof(NetworkManager)} failed to spawn {scene} at {parentPath}. Container node (parent) not found.");
		}
		if (parent.GetMultiplayerAuthority() != this.Multiplayer.GetRemoteSenderId()) {
			throw new Exception($"{nameof(NetworkManager)} failed to spawn {scene} at {parentPath}. Only multiplayer authority can spawn nodes.");
		}
        Node instance = scene.Instantiate();
		if (instance is not NetworkSpawnableNode netNode) {
			instance.Free();
			throw new Exception($"{nameof(NetworkManager)} failed to spawn {scene} at {parentPath}. The instance is not a {nameof(NetworkSpawnableNode)}.");
		}
		netNode.NetIdVariant = netId;
		netNode.Name = name.VariantType == Variant.Type.String
			? name.AsString()
			: netNode.NetId.ToString();
		this._networkSpawnedNodesById[netNode.NetId] = netNode;
		parent.AddChild(netNode);
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Spawned new network node.", new { netNode.NetId, Path = netNode.GetPath() });
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcNotifyNetworkReady(Variant netIdVariant, Godot.Collections.Array args)
	{
		Guid netId = new Guid(netIdVariant.AsByteArray());
		if (!this.NetworkNodesById.TryGetValue(netId, out NetworkSpawnableNode? netNode)) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(NetworkManager),
				"Received NetworkReady notification but could not found network node for the given NetId.",
				new { netId }
			);
			return;
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Received NetworkReady notification for a network node.", new { netNode.NetId, Path = netNode.GetPath() });
		netNode.NotifyNetworkReady([..args]);
	}
}
