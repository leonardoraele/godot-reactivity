using System.Threading.Tasks;
using Godot;

namespace Raele.GodotReactivity;

public partial class SceneSynchronizationManager : Node
{
    // -----------------------------------------------------------------------------------------------------------------
    // STATICS
    // -----------------------------------------------------------------------------------------------------------------

    // public static readonly string MyConstant = "";

    // -----------------------------------------------------------------------------------------------------------------
    // EXPORTS
    // -----------------------------------------------------------------------------------------------------------------

    // [Export] public

    // -----------------------------------------------------------------------------------------------------------------
    // FIELDS
    // -----------------------------------------------------------------------------------------------------------------

    public bool SynchronizationEnabled { get; private set; } = false;
	public Node? SynchronizedScene = null;
	private string? SynchronizedSceneFilePath = null;
	private Variant[]? SynchronizedSceneArguments = [];
    private string? FallbackSceneFilePath;

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	public Node? CurrentScene => this.SynchronizationEnabled
		? this.SynchronizedScene
		: this.GetTree().CurrentScene;

	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------

	[Signal] public delegate void PeerChangedSceneEventHandler(ConnectedPeer peer);
	// [Signal] public delegate void SceneSynchronizationFailureEventHandler(); // TODO

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
		this.Name = nameof(NetworkManager.Scenes);
		this.SetupCurrentSceneObserver();
		NetworkManager.Connectivity.PeerConnected += this.OnPeerConnected;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		NetworkManager.Connectivity.PeerConnected -= this.OnPeerConnected;
	}

    // public override void _Ready()
    // {
    // 	base._Ready();
    // }

    public override void _Process(double delta)
	{
		base._Process(delta);
		this.Multiplayer.Poll();
	}

	// public override void _PhysicsProcess(double delta)
	// {
	// 	base._PhysicsProcess(delta);
	// }

	// public override string[] _GetConfigurationWarnings()
	// 	=> base._PhysicsProcess(delta);

	// -----------------------------------------------------------------------------------------------------------------
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	private void OnPeerConnected(ConnectedPeer peer)
	{
		this.RpcId(peer.Id, MethodName.RpcNotifyPeerSceneChanged, this.CurrentScene?.GetPath() ?? new Variant());
		if (this.IsMultiplayerAuthority()) {
			this.RpcId(
				peer.Id,
				MethodName.RpcChangeSynchronizedSceneToFile,
				this.SynchronizedSceneFilePath ?? "",
				new Godot.Collections.Array(this.SynchronizedSceneArguments ?? [])
			);
		}
	}

	// -----------------------------------------------------------------------------------------------------------------
	// SCENE OBSERVATION METHODS
	// -----------------------------------------------------------------------------------------------------------------

	private void SetupCurrentSceneObserver()
	{
		ulong? lastSceneId = this.CurrentScene?.GetInstanceId();
		this.GetTree().TreeChanged += () => {
			ulong? currentSceneId = this.CurrentScene?.GetInstanceId();
			if (/*currentSceneId.HasValue &&*/ currentSceneId != lastSceneId) {
				lastSceneId = currentSceneId;
				if (
					NetworkManager.Instance.Status.Value == NetworkManager.ConnectionStateEnum.Host
					|| NetworkManager.Instance.Status.Value == NetworkManager.ConnectionStateEnum.ClientConnected
				) {
					this.Rpc(MethodName.RpcNotifyPeerSceneChanged, this.CurrentScene?.GetPath() ?? new Variant());
				}
			}
		};
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcNotifyPeerSceneChanged(NodePath scenePath)
	{
		if (!NetworkManager.Instance.ConnectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(NetworkManager),
				"Received peer scene change notification from an unknown peer."
			);
			return;
		}
		string? scenePathStr = scenePath.IsEmpty ? null : scenePath;
		string fromScene = peer.CurrentScene.Value?.IsEmpty != false ? "null" : peer.CurrentScene.Value;
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), $"Peer #{peer.Id} changed scenes.", fromScene, "->", scenePathStr ?? "null");
		peer.CurrentScene.Value = scenePathStr == null ? null : new NodePath(scenePathStr);
		this.EmitSignal(SignalName.PeerChangedScene, peer);
	}

	// -----------------------------------------------------------------------------------------------------------------
	// SCENE SYNCHRONIZATION METHODS
	// -----------------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Change the current scene to the synchronized scene and start scene synchronization. The synchronized scene is
	/// the last scene the authority used to call ChangeSynchronizedSceneToFile(). While scene synchronization is
	/// enabled, the current scene will automatically change whenever the authority calls
	/// ChangeSynchronizedSceneToFile(). If ChangeSynchronizedSceneToFile() has never been called, the current local
	/// scene will remain null until the authority calls ChangeSynchronizedSceneToFile() or scene synchronization is
	/// stopped.
	/// Scene synchronization is automatically stopped if the scene is changed manually. (i.e. if SceneTree.CurrentScene
	/// changes, e.g. by calling SceneTree.ChangeSceneToFile)
	/// You can listen to the SceneSynchronizationFailure signal be notified when scene synchronization is stopped
	/// because of connection loss, so you can.
	///
	/// // TODO
	/// If you pass a `root` node, the synchronized scene will be added to that node instead of the tree root node. This
	/// is useful if you want the synchronized scene to take place inside of the normal scene (e.g. in a viewport) or
	/// outside of it (e.g. side by side with the normal current scene in the scene tree).
	/// // TODO (alt name: `parallelScene`)
	/// If `setCurrentScene` is false, SceneManager won't free or change this.GetTree().CurrentScene when changing the
	/// synchronized scene. In this case, changing the scene via SceneTree.ChangeSceneToFile won't cause synchronization
	/// to end. This is useful if you want the authority to be able to change to another scene without ending scene
	/// synchronization. (i.e. without stopping connected peers from synchronizing states in with each other)
	/// Use
	/// SceneManager.SynchronizedScene to access the synchronized scene.
	/// </summary>
	public void StartSynchronization(string? fallbackSceneFilePath = null)
	{
		this.FallbackSceneFilePath = fallbackSceneFilePath;
		this.SynchronizationEnabled = true;
		this.SynchronizedScene = this.GetTree().CurrentScene;
		this.GetTree().TreeChanged += this.CheckSceneChanged;
		this.ChangeToSynchronizedScene();
	}

	private void CheckSceneChanged()
	{
		if (this.GetTree().CurrentScene != this.SynchronizedScene) {
			Callable.From(() => {
				if (this.SynchronizationEnabled && this.GetTree().CurrentScene != this.SynchronizedScene) {
					this.StopSynchronization();
				}
			}).CallDeferred();
		}
	}

	public void StopSynchronizationAndFallBack(string? fallbackSceneFilePath = null)
	{
		this.StopSynchronization();
		fallbackSceneFilePath ??= this.FallbackSceneFilePath;
		if (fallbackSceneFilePath != null) {
			this.GetTree().ChangeSceneToFile(fallbackSceneFilePath);
			this.FallbackSceneFilePath = null;
		}
	}

	public void StopSynchronization()
	{
		if (!this.SynchronizationEnabled) {
			return;
		}
		this.SynchronizationEnabled = false;
		this.SynchronizedScene = null;
		this.SynchronizedSceneArguments = [];
		this.GetTree().TreeChanged -= this.CheckSceneChanged;
	}

	private async void ChangeToSynchronizedScene()
	{
		string? filePath = this.SynchronizedSceneFilePath;
		await this.RemoveAndFreeCurrentScene();
		if (this.GetTree().CurrentScene != null || filePath != this.SynchronizedSceneFilePath) {
			return;
		}
		if (this.SynchronizedSceneFilePath == null) {
			GD.PushWarning(
				NetworkManager.NetId,
				nameof(SceneSynchronizationManager),
				"Cannot change to synchronized scene because no scene file path is set."
			);
			return;
		}
		this.SynchronizedScene = GD.Load<PackedScene>(this.SynchronizedSceneFilePath)?.Instantiate();
		if (this.SynchronizedScene == null) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(SceneSynchronizationManager),
				"Failed to load scene from file.",
				"Scene: ", this.SynchronizedSceneFilePath
			);
			return;
		}
		this.GetTree().Root.AddChild(this.SynchronizedScene);
		this.GetTree().CurrentScene = this.SynchronizedScene;
		if (this.SynchronizedScene.HasMethod("_NetworkReady")) {
			this.SynchronizedScene.Call("_NetworkReady", this.SynchronizedSceneArguments);
		}
	}

	public async Task RemoveAndFreeCurrentScene()
	{
		if (this.SynchronizedScene == null) {
			return;
		}
		Node synchronizedScene = this.SynchronizedScene;
		this.SynchronizedScene = null;
		this.GetTree().CurrentScene = null;
		synchronizedScene.GetParent()?.RemoveChild(synchronizedScene);
		TaskCompletionSource source = new();
		Callable.From(() => {
			synchronizedScene.Free();
			source.SetResult();
		}).CallDeferred();
		await source.Task;
	}

	/// <summary>
	/// Change the scene of all peers with synchronized scene enabled (including local peer) to the scene at the given
	/// path. If the local peer is not synchronizing scenes, this method will still change the synchronized scene of
	/// other peers, but the local peer will only go to the new scene if it starts scene synchronization.
	/// </summary>
	public void ChangeSynchronizedSceneToFile(string scenePath, params Variant[] arguments)
	{
		if (!this.IsMultiplayerAuthority()) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(SceneSynchronizationManager),
				"Only the authority can change scenes.",
				"Scene: ", scenePath
			);
			return;
		}
		this.Rpc(MethodName.RpcChangeSynchronizedSceneToFile, scenePath, new Godot.Collections.Array(arguments));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void RpcChangeSynchronizedSceneToFile(string scenePath, Godot.Collections.Array<Variant> args)
	{
		this.SynchronizedSceneFilePath = string.IsNullOrEmpty(scenePath) ? null : scenePath;
		this.SynchronizedSceneArguments = [..args];
		GD.PrintS(NetworkManager.NetId, nameof(SceneSynchronizationManager), "Synchronized scene changed to:", scenePath, "with args:", args);
		if (this.SynchronizationEnabled) {
			this.ChangeToSynchronizedScene();
		}
	}
}
