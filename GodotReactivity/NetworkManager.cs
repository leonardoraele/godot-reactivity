using System;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public static NetworkManager Instance { get; private set; } = null!;
	// public static string NetId = string.Join("", Guid.NewGuid().ToString().TakeLast(12));
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
}
