using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Godot;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public const string SERVER_BIND_ADDRESS = "*";
	public const int DEFAULT_PORT = 4000;

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	public ReactiveState<ConnectionStateEnum> Status = new(ConnectionStateEnum.Offline);
    public ConnectedPeer LocalPeer { get; private set; } = null!; // This is set in _EnterTree()
	private ReactiveDictionary<long, ConnectedPeer> _connectedPeers = new();

    // -----------------------------------------------------------------------------------------------------------------
    // PROPERTIES
    // -----------------------------------------------------------------------------------------------------------------

	public bool Online => (((int) this.Status.Value) & ((int) ConnectionStateEnum._OnlineFlag)) != 0;
    public IReadOnlyDictionary<long, ConnectedPeer> ConnectedPeers => this._connectedPeers;
	public IEnumerable<ConnectedPeer> RemotePeers => this.ConnectedPeers.Values
		.Where(peer => peer != this.LocalPeer);

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

	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Represents the state of the network connection.
	///
	/// First 4 bits determines the type of the connection: if 0 = is offline, 1 = is server, 2 = is client.
	/// Bits 5~8 determine the state of the connection: 0 = offline, 1 = connecting, 2 = connected/online.
	/// </summary>
	public enum ConnectionStateEnum: int {
		Offline = 0,
		_OnlineFlag = 2 << 4,
		Hosting = 1 + (2 << 4),
		ClientConnecting = 2 + (1 << 4),
		ClientConnected = 2 + (2 << 4),
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

    public void OpenMultiplayerServer(int port = DEFAULT_PORT)
	{
		this.Disconnect();
		WebSocketMultiplayerPeer wsServerPeer = new WebSocketMultiplayerPeer();
		wsServerPeer.CreateServer(port, SERVER_BIND_ADDRESS);
		this.Multiplayer.MultiplayerPeer = wsServerPeer;
		this.Multiplayer.PeerConnected += this.OnPeerConnected;
		this.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
		this.UpdateLocalPeer();
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Server started.");
		GD.PushWarning(nameof(this.OpenMultiplayerServer));
		this.Status.Value = ConnectionStateEnum.Hosting;
		this.EmitSignal(SignalName.ServerOpened);
	}

	public async Task ConnectToServer(string connectAddress)
	{
		this.Disconnect();
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
		this.Status.Value = ConnectionStateEnum.ClientConnecting;
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
		GD.PushWarning(nameof(ConnectToServer));
		this.UpdateLocalPeer();
		this.Status.Value = ConnectionStateEnum.ClientConnected;
		this.EmitSignal(SignalName.ConnectedToServer);
	}

    private void UpdateLocalPeer()
	{
		long id = this.Multiplayer.GetUniqueId();
		if (this.LocalPeer != null && this.LocalPeer.Id != id) {
			this._connectedPeers.Remove(this.LocalPeer.Id);
		}
		this.LocalPeer = this._connectedPeers[id] = new() {
			Id = id,
			CurrentScene = new(this.GetTree()?.CurrentScene?.SceneFilePath ?? ""),
		};
	}

    private void OnPeerConnected(long peerId)
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), $"Peer #{peerId} connected.");
		ConnectedPeer peer = this._connectedPeers[peerId] = new() { Id = peerId };
		this.EmitSignal(SignalName.PeerConnected, peer);
	}

	private void OnPeerDisconnected(long id)
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), $"Peer #{id} disconnected.");
		ConnectedPeer peer = this._connectedPeers[id];
		this._connectedPeers.Remove(id);
		this.EmitSignal(SignalName.PeerDisconnected, peer);
	}

	public void Disconnect()
	{
		switch (this.Status.Value) {
			case ConnectionStateEnum.Hosting:
				this.CloseMultiplayerServer();
				break;
			case ConnectionStateEnum.ClientConnected:
			case ConnectionStateEnum.ClientConnecting:
				this.DisconnectFromServer();
				break;
			default:
				this._BaseDisconnect();
				break;
		}
	}

	private void CloseMultiplayerServer()
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Server closed.");
		this._BaseDisconnect();
		this.Multiplayer.PeerConnected -= this.OnPeerConnected;
		this.Multiplayer.PeerDisconnected -= this.OnPeerDisconnected;
		this.EmitSignal(SignalName.ServerClosed);
	}

	private void DisconnectFromServer()
	{
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), "Disconnected from server.");
		this._BaseDisconnect();
		this.Multiplayer.PeerConnected -= this.OnPeerConnected;
		this.Multiplayer.PeerDisconnected -= this.OnPeerDisconnected;
		this.Multiplayer.ServerDisconnected -= this.DisconnectFromServer;
		this.EmitSignal(SignalName.DisconnectedFromServer);
	}

	private void _BaseDisconnect()
	{
		this._connectedPeers.Clear();
		this.Multiplayer.MultiplayerPeer?.Close();
		this.Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		this.Status.Value = ConnectionStateEnum.Offline;
		this.UpdateLocalPeer();
	}
}
