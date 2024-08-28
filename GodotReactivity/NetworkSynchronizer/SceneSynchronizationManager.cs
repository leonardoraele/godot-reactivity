using System;
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

	// Because this class calls this.GetTree() so many times, we cache a reference to the SceneTree instead.
	private SceneTree? TreeCache;

	/// <summary>
	/// This field determines whether the local peer is currently synchronizing scenes with the authority. If true, the
	/// local peer will automatically change scene to the synchronized scene (i.e. the scene the authority peer is on)
	/// whenever the authority changes the synchronized scene. If false, the local peer will still keep track of the
	/// synchronized scene, but it will not automatically change to it. Call StartSynchronization() to enable scene
	/// synchronization.
	/// If the local peer changes scenes manually (e.g. by calling SceneTree.ChangeSceneToFile), scene synchronization
	/// will be stopped automatically.
	/// </summary>
	public bool SynchronizationEnabled { get; private set; } = false;

	/// <summary>
	/// When scene synchronization starts (by calling StartSynchronization()), it is possible to configure a fallback
	/// scene for when synchronization is interrupted. This field stores the path to the fallback scene. If scene
	/// synchronization is ended by calling StopSynchronizationAndFallBack(), the local peer will change to this scene.
	/// </summary>
	private string? FallbackSceneFilePath;

	// These two fields are always in sync with the authority so that the local peer can synchronize and desynchronize
	// scenes with the authority (i.e. change to the same scene as the authority) whenever they want.
	private string SynchronizedSceneFilePath = "";
	private Variant[]? SynchronizedSceneArguments = [];

	// Used internally to know when the current scene is being changed by the SceneSynchronizationManager. This is
	// necessary to distinguish when the current scene is changed by the SceneSynchronizationManager itself from when it
	// is changed manually by the user, so that we know when to stop scene synchronization.
	private bool ChangingToSynchronizedScene = false;

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	// public

	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------

	[Signal] public delegate void PeerChangedSceneEventHandler(ConnectedPeer peer, string previousScene);

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
		this.TreeCache = this.GetTree();
		this.Name = nameof(NetworkManager.Scenes);
		this.SetupSceneObservation();
		NetworkManager.Connectivity.PeerConnected += this.OnPeerConnected;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		this.TreeCache = null;
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

	private void OnPeerConnected(ConnectedPeer peer)
	{
		this.NotifyPeerOfLocalScene(peer);
		if (this.IsMultiplayerAuthority()) {
			this.NotifyPeerOfSynchronizedScene(peer);
		}
	}

	// -----------------------------------------------------------------------------------------------------------------
	// SCENE OBSERVATION METHODS
	// -----------------------------------------------------------------------------------------------------------------

	private void SetupSceneObservation() => this.TreeCache!.TreeChanged += this.CheckSceneChanged;

	private void CheckSceneChanged()
	{
		if (
			NetworkManager.Connectivity.LocalPeer.CurrentScene.Value
			!= (this.TreeCache?.CurrentScene?.SceneFilePath ?? "")
		) {
			this.NotifySceneChanged();
		}
	}

	private void NotifySceneChanged()
		=> this.Rpc(MethodName.RpcNotifyPeerChangedScene, this.TreeCache?.CurrentScene?.SceneFilePath ?? "");

	private void NotifyPeerOfLocalScene(ConnectedPeer peer)
		=> this.RpcId(peer.Id, MethodName.RpcNotifyPeerChangedScene, this.TreeCache?.CurrentScene?.SceneFilePath ?? "");

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void RpcNotifyPeerChangedScene(string sceneFilePath)
	{
		if (!NetworkManager.Instance.ConnectedPeers.TryGetValue(this.Multiplayer.GetRemoteSenderId(), out ConnectedPeer? peer)) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(NetworkManager),
				"Received peer scene change notification from an unknown peer."
			);
			return;
		}
		GD.PrintS(NetworkManager.NetId, nameof(NetworkManager), $"🔀 Peer #{peer.Id} changed scene: '{peer.CurrentScene.Value}' -> '{sceneFilePath}'");
		string previousScene = peer.CurrentScene.Value;
		peer.CurrentScene.Value = sceneFilePath;
		this.EmitSignal(SignalName.PeerChangedScene, peer, previousScene);
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
	/// If `setCurrentScene` is false, SceneManager won't free or change this.TreeCache?.CurrentScene when changing the
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
		this.PeerChangedScene += this.OnPeerChangedScene;
		this.ChangeToSynchronizedScene();
	}

	private void OnPeerChangedScene(ConnectedPeer peer, string previousScene)
	{
		if (
			!this.ChangingToSynchronizedScene
			&& peer.IsLocalPeer
			&& this.SynchronizationEnabled
			&& this.SynchronizedSceneFilePath != this.TreeCache?.CurrentScene?.SceneFilePath
		) {
			this.StopSynchronization();
		}
	}

	/// <summary>
	/// Stops scene synchronization and changes the current scene to the fallback scene. If no fallback scene is set,
	/// the current scene will not change.
	/// </summary>
	public void StopSynchronizationAndFallBack()
	{
		if (!this.SynchronizationEnabled) {
			return;
		}
		string? fallbackSceneFilePath = this.FallbackSceneFilePath;
		this.StopSynchronization();
		if (!string.IsNullOrEmpty(fallbackSceneFilePath)) {
			this.TreeCache?.ChangeSceneToFile(this.FallbackSceneFilePath);
		}
	}

	/// <summary>
	/// Stops scene synchronization without changing the current scene. The local peer will still keep track of the
	/// synchronized scene set by the authority, but it will not automatically change to it. Call StartSynchronization()
	/// to enable scene synchronization again.
	/// This method is called automatically if the local peer changes scenes manually (e.g. by calling
	/// SceneTree.ChangeSceneToFile()).
	/// </summary>
	public void StopSynchronization()
	{
		if (!this.SynchronizationEnabled) {
			return;
		}
		this.SynchronizationEnabled = false;
		this.FallbackSceneFilePath = null;
		this.PeerChangedScene -= this.OnPeerChangedScene;
	}

	private async void ChangeToSynchronizedScene()
	{
		try {
			this.ChangingToSynchronizedScene = true;
			await this._ChangeToSynchronizedScene();
		} finally {
			this.ChangingToSynchronizedScene = false;
		}
	}

	private async Task _ChangeToSynchronizedScene()
	{
		if (this.TreeCache?.CurrentScene?.SceneFilePath == this.SynchronizedSceneFilePath) {
			return;
		}

		// Exit current scene
		{
			string sceneFilePath = this.SynchronizedSceneFilePath;
			await this.RemoveAndFreeCurrentScene();

			// Check if all conditions to change scene are still valid. (either might have changed while waiting)
			if (
				this.TreeCache == null
				|| this.TreeCache.CurrentScene != null
				|| sceneFilePath != this.SynchronizedSceneFilePath
			) {
				return;
			}
		}

		// If the synchronized scene is empty, then there's nothing more to do.
		if (string.IsNullOrWhiteSpace(this.SynchronizedSceneFilePath)) {
			return;
		}

		// Load next scene and add it to the tree
		Node? enteringScene = GD.Load<PackedScene>(this.SynchronizedSceneFilePath)?.Instantiate();
		if (enteringScene == null) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(SceneSynchronizationManager),
				"Failed to change to synchronized scene.",
				"Cause: Failed to load scene from file.",
				"SceneFilePath: ", this.SynchronizedSceneFilePath
			);
			return;
		}
		try {
			if (enteringScene.HasMethod("_before_enter_tree")) {
				enteringScene.Call("_before_enter_tree", this.SynchronizedSceneArguments);
			} else if (enteringScene.HasMethod("_BeforeEnterTree")) {
				enteringScene.Call("_BeforeEnterTree", this.SynchronizedSceneArguments);
			}
		} catch (Exception e) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(SceneSynchronizationManager),
				"Failed to change to synchronized scene.",
				"Cause: Scene threw an exception on _before_enter_tree or _BeforeEnterTree.",
				"SceneFilePath: ", this.SynchronizedSceneFilePath,
				"Exception: ", e
			);
			return;
		}
		this.TreeCache.Root.AddChild(enteringScene);
		this.TreeCache.CurrentScene = enteringScene;
		// Because of [this issue](https://github.com/godotengine/godot/issues/96195), we must manually call for
		// CheckSceneChanged(), since it is normally only called when the tree changes, but the issue prevents us from
		// updating the current scene before that. This also means that, until the issue is fixed, we can't have
		// SceneTree.CurrentScene point to the new scene at _EnterTree() and _Ready(), which means that any code that
		// relies on the current scene (e.g. spawning network nodes) must be deferred to the end of the frame.
		// FIXME // TODO Check if the issue has already been fixed and update this code accordingly.
		this.CheckSceneChanged();
	}

	public async Task RemoveAndFreeCurrentScene()
	{
		if (this.TreeCache?.CurrentScene == null) {
			return;
		}

		// Remove current scene
		Node exitingScene = this.TreeCache.CurrentScene;
		exitingScene.GetParent()?.RemoveChild(exitingScene);
		this.TreeCache.CurrentScene = null;

		// Wait for the end of the frame then free the scene
		TaskCompletionSource source = new();
		Callable.From(() => {
			exitingScene.Free();
			source.SetResult();
		}).CallDeferred();
		await source.Task;

		// Alternative implementation for freeing the scene:
		// TaskCompletionSource source = new();
		// void CheckSceneIsStillValid() {
		// 	if (!exitingScene.IsInstanceValid()) {
		// 		source.SetResult();
		// 	}
		// }
		// this.TreeCache.ProcessFrame += CheckSceneIsStillValid;
		// exitingScene.QueueFree();
		// await source.Task;
		// this.TreeCache.ProcessFrame -= CheckSceneIsStillValid;
	}

	private void NotifyPeerOfSynchronizedScene(ConnectedPeer peer)
		=> this.RpcId(
			peer.Id,
			MethodName.RpcChangeSynchronizedScene,
			this.SynchronizedSceneFilePath,
			new Godot.Collections.Array(this.SynchronizedSceneArguments ?? [])
		);

	/// <summary>
	/// Change the scene of all peers with synchronized scene enabled (including local peer) to the scene at the given
	/// path. If the local peer is not synchronizing scenes, this method will still change the synchronized scene of
	/// other peers, but the local peer will only go to the new scene if it starts scene synchronization.
	/// </summary>
	public void ChangeSynchronizedScene(string sceneFilePath, params Variant[] arguments)
	{
		if (!this.IsMultiplayerAuthority()) {
			GD.PushError(
				NetworkManager.NetId,
				nameof(SceneSynchronizationManager),
				"Only the authority can change scenes.",
				"Scene: ", sceneFilePath
			);
			return;
		}
		this.Rpc(MethodName.RpcChangeSynchronizedScene, sceneFilePath, new Godot.Collections.Array(arguments));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void RpcChangeSynchronizedScene(string sceneFilePath, Godot.Collections.Array args)
	{
		this.SynchronizedSceneFilePath = sceneFilePath;
		this.SynchronizedSceneArguments = [..args];
		GD.PrintS(
			NetworkManager.NetId,
			nameof(SceneSynchronizationManager),
			"🌐 Synchronized scene changed to:", $"'{sceneFilePath}'",
			"with args:", args
		);
		if (this.SynchronizationEnabled) {
			this.ChangeToSynchronizedScene();
		}
	}
}
