using Godot;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
    // -----------------------------------------------------------------------------------------------------------------
    // STATICS
    // -----------------------------------------------------------------------------------------------------------------

    public static NetworkManager Instance { get; private set; } = null!; // TODO Make private
	public static SceneSynchronizationManager Scenes => NetworkManager.Instance._scenes;
	public static RpcUtilityManager RpcUtil => NetworkManager.Instance._rpcUtil;
	public static NetworkManager Connectivity => NetworkManager.Instance; // TODO Turn into ConnectivityManager class
	public static NetworkManager Spawner => NetworkManager.Instance; // TODO Turn into NodeSpawningManager class
	public static string NetId => NetworkManager.Instance.Multiplayer?.HasMultiplayerPeer() == true
		? NetworkManager.Instance.Multiplayer.GetUniqueId() == 1
			? "🌐#1"
			: $"💻#{NetworkManager.Instance.Multiplayer.GetUniqueId()}"
		: string.Empty;

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

    private SceneSynchronizationManager _scenes = new();
    private RpcUtilityManager _rpcUtil = new();

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
		if (NetworkManager.Instance != null) {
			GD.PushError($"Failed to set {nameof(NetworkManager)}.{nameof(NetworkManager.Instance)} because it is already set.");
			this.QueueFree();
			return;
		}
		NetworkManager.Instance = this;
		this.Disconnect();
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (NetworkManager.Instance == this) {
			NetworkManager.Instance = null!;
		}
	}

	public override void _Ready()
	{
		base._Ready();
		this.AddChild(this._scenes);
		this.AddChild(this._rpcUtil);
		this.SetupSpawns();
	}

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
	// SETUP METHODS
	// -----------------------------------------------------------------------------------------------------------------

}
