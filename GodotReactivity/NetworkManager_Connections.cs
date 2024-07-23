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
    public ConnectedPeer? LocalPeer { get; private set; } // null if offline; never null while online // TODO Should be reactive
	private ReactiveDictionary<long, ConnectedPeer> _connectedPeers = new();

    // -----------------------------------------------------------------------------------------------------------------
    // PROPERTIES
    // -----------------------------------------------------------------------------------------------------------------

    public IReadOnlyDictionary<long, ConnectedPeer> ConnectedPeers => this._connectedPeers;
	public IEnumerable<ConnectedPeer> RemotePeersInScene
	{
		get {
			NodePath? currentScene = this.LocalPeer?.CurrentScene.Value;
			if (currentScene == null) { // Either this is offline or not in any scene
				return [];
			}
			return this._connectedPeers.Values
				.Where(peer => peer.Id != this.LocalPeer?.Id)
				.Where(peer => currentScene == peer.CurrentScene.Value);
		}
	}

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
			ulong? currentSceneId = this.GetTree().CurrentScene?.GetInstanceId();
			if (currentSceneId != lastSceneId) {
				lastSceneId = currentSceneId;
				if (
					this.ConnectionState.Value == ConnectionStateEnum.Host
					|| this.ConnectionState.Value == ConnectionStateEnum.ClientConnected
				) {
					this.Rpc(MethodName.RpcNotifyPeerSceneChanged, this.GetTree().CurrentScene?.GetPath() ?? new Variant());
				}
			}
		};
	}

    public void OpenMultiplayerServer(int port = DEFAULT_PORT)
	{
		WebSocketMultiplayerPeer wsServerPeer = new WebSocketMultiplayerPeer();
		wsServerPeer.CreateServer(port, SERVER_BIND_ADDRESS);
		this.Multiplayer.MultiplayerPeer = wsServerPeer;
		this.Multiplayer.PeerConnected += this.OnPeerConnected;
		this.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
		this.AddPeerForSelf();
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Server started.");
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
		this.Multiplayer.ServerDisconnected += this.DisconnectFromServer;
		this.ConnectionState.Value = ConnectionStateEnum.ClientConnecting;
		try {
			await source.Task;
			GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Connected successfully.");
		} catch (Exception e) {
			GD.PrintErr(nameof(NetworkManager), " Failed to connect to server. ", e.Message);
			this.DisconnectFromServer();
			throw;
		} finally {
			this.Multiplayer.ConnectedToServer -= source.SetResult;
			this.Multiplayer.ConnectionFailed -= source.SetCanceled;
		}
		this.AddPeerForSelf();
		// this.BroadcastCurrentScene();
		this.ConnectionState.Value = ConnectionStateEnum.ClientConnected;
		this.EmitSignal(SignalName.ConnectedToServer);
	}

    private void AddPeerForSelf()
	{
		long id = this.Multiplayer.GetUniqueId();
		this.LocalPeer = this._connectedPeers[id] = new() {
			Id = id,
			CurrentScene = new(this.GetTree().CurrentScene.GetPath())
		};
	}

    private void OnPeerConnected(long peerId)
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Peer connected.", peerId);
		ConnectedPeer peer = this._connectedPeers[peerId] = new() { Id = peerId };
		if (this.GetTree().CurrentScene?.GetPath() is NodePath currentScene) {
			this.RpcId(peerId, MethodName.RpcNotifyPeerSceneChanged, currentScene);
		}
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
		this.Multiplayer.ServerDisconnected -= this.DisconnectFromServer;
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcNotifyPeerSceneChanged(NodePath? scenePath)
	{
		if (!this._connectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(NetworkManager),
				"Received peer scene change notification from an unknown peer.",
				new {
					RemoveSenderId = this.Multiplayer.GetRemoteSenderId(),
					ConnectedPeers = this._connectedPeers.Keys
				}
			);
			return;
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Connected peer changed scene.", "Peer Id:", peer.Id, "To Scene:", scenePath ?? "null", "From Scene:", peer.CurrentScene.Value ?? "null");
		peer.CurrentScene.Value = scenePath;
		this.OnPeerSceneChanged(peer);
		this.EmitSignal(SignalName.PeerSceneChanged, peer);
	}
}
