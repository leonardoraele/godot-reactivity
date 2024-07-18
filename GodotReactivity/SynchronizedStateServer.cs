using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Raele.PocketWars;

namespace Raele.GodotReactivity;

public partial class SynchronizedStateServer : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public const string SERVER_BIND_ADDRESS = "*";
	public const int DEFAULT_PORT = 3000;

	public static SynchronizedStateServer Instance { get; private set; } = null!;
	public static string NetId = string.Join("", Guid.NewGuid().ToString().TakeLast(8));

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	private ReactiveDictionary<long, ConnectedPeer> _connectedPeers = new();
	public ReactiveState<ConnectionStateEnum> ConnectionState = new(ConnectionStateEnum.Offline);

	// Fields for Bi-Directional RPC control
	private Dictionary<int, TaskCompletionSource<Variant>> DataRequests = new();
	private int lastDataRequestId = 0;

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	public IReadOnlyDictionary<long, ConnectedPeer> ConnectedPeers => this._connectedPeers;
	public IEnumerable<ConnectedPeer> PeersInScene
	{
		get {
			NodePath currentScene = this.GetTree().CurrentScene.GetPath();
			return this._connectedPeers.Values.Where(peer => currentScene == peer.CurrentScene.Value);
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

	public override void _EnterTree()
	{
		base._EnterTree();
		this.SetupSingletonInstance();
		this.SetupSceneChangeObservation();
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

	private void SetupSingletonInstance()
	{
		if (SynchronizedStateServer.Instance != null) {
			GD.PushError($"Failed to set {nameof(SynchronizedStateServer)}.{nameof(SynchronizedStateServer.Instance)} because it is already set.");
			this.QueueFree();
			return;
		}
		SynchronizedStateServer.Instance = this;
		this.TreeExiting += () => {
			if (SynchronizedStateServer.Instance == this) {
				SynchronizedStateServer.Instance = null!;
			}
		};
	}

	private void SetupSceneChangeObservation()
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

    // -----------------------------------------------------------------------------------------------------------------
    // CONNECTION METHODS
    // -----------------------------------------------------------------------------------------------------------------

    public void OpenMultiplayerServer(int port = DEFAULT_PORT)
	{
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Server started.");
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
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Connecting to server...", new { connectAddress });
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
			GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Connected successfully.");
		} catch {
			GD.PrintErr(nameof(SynchronizedStateServer), "Failed to connect to server.");
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
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Peer connected.", peerId);
		this._connectedPeers[peerId] = new() { Id = peerId };
		this.EmitSignal(SignalName.PeerConnected, peerId);
	}

	private void OnPeerDisconnected(long id)
	{
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Peer disconnected.", id);
		this._connectedPeers.Remove(id);
		this.EmitSignal(SignalName.PeerDisconnected, id);
	}

	public void CloseMultiplayerServer()
	{
		if (this.ConnectionState.Value == ConnectionStateEnum.Offline) {
			return;
		}
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Server closed.");
		this.EndConnection();
		this.EmitSignal(SignalName.ServerClosed);
	}

	public void DisconnectFromServer()
	{
		if (this.ConnectionState.Value == ConnectionStateEnum.Offline) {
			return;
		}
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Disconnected from server.");
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
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Broadcasting scene change... To Scene:", currentScene);
		this.Rpc(MethodName.RpcNotifyPeerSceneChanged, currentScene ?? new Variant());
    }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcNotifyPeerSceneChanged(NodePath? scenePath)
	{
		if (!this._connectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)) {
			GD.PushError(
				nameof(SynchronizedStateServer),
				"Received peer scene change notification from an invalid peer.",
				new {
					RemoveSenderId = this.Multiplayer.GetRemoteSenderId(),
					ConnectedPeers = this._connectedPeers.Keys
				}
			);
			return;
		}
		GD.PrintS(SynchronizedStateServer.NetId, nameof(SynchronizedStateServer), "Remote peer changed scene.", "Peer Id:", peer.Id, "To Scene:", scenePath);
		peer.CurrentScene.Value = scenePath;
		this.EmitSignal(SignalName.PeerSceneChanged, peer);
	}

	// -----------------------------------------------------------------------------------------------------------------
	// BI-DIRECTIONAL RPC METHODS
	// -----------------------------------------------------------------------------------------------------------------

	public async Task<Variant> SendRpcRequest(long peerId, Node node, StringName method, params Variant[] args)
		=> await this.SendRpcRequest(peerId, node.GetPath(), method, args);

	public Task<Variant> SendRpcRequest(long peerId, NodePath path, StringName method, params Variant[] args)
	{
		int id = ++lastDataRequestId;
		TaskCompletionSource<Variant> source = new();
		CancellationTokenSource cancel = new(5000);
		this.DataRequests[id] = source;
		this.RpcId(peerId, MethodName.RpcHandleRequest, id, path, method, new Godot.Collections.Array(args));
		try {
			return source.Task.WaitAsync(cancel.Token);
		} finally {
			cancel.Dispose();
			this.DataRequests.Remove(id);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleRequest(int id, NodePath path, StringName method, Godot.Collections.Array args)
	{
		try {
			Variant result = this.GetNode(path).Call(method, args);
			this.RpcId(this.Multiplayer.GetRemoteSenderId(), MethodName.RpcHandleRequestResult, id, result);
		} catch (Exception e) {
			this.RpcId(this.Multiplayer.GetRemoteSenderId(), MethodName.RpcHandleRequestFailure, id, e.ToString());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleRequestResult(int id, Variant result)
	{
		if (this.DataRequests.TryGetValue(id, out TaskCompletionSource<Variant>? source)) {
			source.SetResult(result);
			this.DataRequests.Remove(id);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleRequestFailure(int id, string message)
	{
		if (this.DataRequests.TryGetValue(id, out TaskCompletionSource<Variant>? source)) {
			source.SetException(new Exception(message));
			this.DataRequests.Remove(id);
		}
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
