using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Raele.GodotReactivity;

public partial class NetworkNode : Node
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

	private List<ReactiveVariant> Observables = new();
	private uint DirtyFlag = 0;
	private bool IsUpdatingStates = false;

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------


	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------

	// [Signal] public delegate void EventHandler()

	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------

	[AttributeUsage(AttributeTargets.Field)]
	public class SynchronizedAttribute : Attribute {}

	// -----------------------------------------------------------------------------------------------------------------
	// EVENTS
	// -----------------------------------------------------------------------------------------------------------------

	public NetworkNode() : base() => this.SetupObservables();

	public override void _EnterTree()
	{
		base._EnterTree();
		SynchronizedStateServer.Instance.PeerSceneChanged += OnPeerSceneChanged;
	}

    public override void _ExitTree()
    {
        base._ExitTree();
		SynchronizedStateServer.Instance.PeerSceneChanged -= OnPeerSceneChanged;
    }

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

    private void SetupObservables()
	{
		foreach (FieldInfo field in this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
			foreach (SynchronizedAttribute _attr in field.GetCustomAttributes<SynchronizedAttribute>()) {
				if (field.GetValue(this) is not ReactiveVariant state) {
					GD.PushError($"[{nameof(NetworkNode)}] Failed to synchronize field {field.Name}. Cause: Field is not of type ReactiveVariant.");
				} else {
					this.SynchronizeState(state);
				}
				break;
			}
		}
	}

	private void SynchronizeState(ReactiveVariant state)
	{
		int stateIndex = this.Observables.Count;
		GD.PrintS(SynchronizedStateServer.NetId, "[NetworkNode] Found a synchronized state field. Index:", stateIndex);
		state.Changed += () => {
			if (!this.IsUpdatingStates) {
				this.MarkDirty(stateIndex);
			}
		};
		this.Observables.Add(state);
	}

	private void OnPeerSceneChanged(ConnectedPeer peer)
	{
		GD.PrintS(SynchronizedStateServer.NetId, nameof(NetworkNode), nameof(OnPeerSceneChanged), new { peerId = peer.Id, IsMultiplayerAuthority = IsMultiplayerAuthority(), LocalCurrentScene = this.GetTree().CurrentScene?.GetPath(), PeerCurrentScene = peer.CurrentScene.Value });
		if (this.IsMultiplayerAuthority() && this.GetTree().CurrentScene.GetPath() == peer.CurrentScene.Value) {
			// TODO Since we are marking all states as dirty, the states will be sent to all connected peers. Instead,
			// we should send it only to the peer who changed scene.
			this.MarkDirty(uint.MaxValue);
		}
	}

	private void MarkDirty(int index)
		=> this.MarkDirty(1u << index);

	private void MarkDirty(uint dirtyFlags)
	{
		if (this.DirtyFlag == 0) {
			Callable.From(this.BroadcastStates).CallDeferred();
		}
		this.DirtyFlag |= dirtyFlags;
		GD.PrintS(SynchronizedStateServer.NetId, "[NetworkNode] State dirtied.");
	}

	private void BroadcastStates()
	{
		if (
			this.DirtyFlag == 0
			|| SynchronizedStateServer.Instance.ConnectionState.Value
				== SynchronizedStateServer.ConnectionStateEnum.Offline
		) {
			return;
		}
		Godot.Collections.Array newValues = new(
			this.Observables
				.Where((_, index) => (this.DirtyFlag & (1u << index)) != 0)
				.Select(observable => observable.VariantValue)
		);
		if (newValues.Count != 0) {
			GD.PrintS(SynchronizedStateServer.NetId, "[NetworkNode] Found dirty values to update. Values:", newValues);
			if (this.IsMultiplayerAuthority()) {
				foreach (long peerId in SynchronizedStateServer.Instance.PeersInScene.Select(peer => peer.Id)) {
					GD.PrintS(SynchronizedStateServer.NetId, "[NetworkNode] Sending state update to peer. Id:", peerId);
					this.RpcId(peerId, MethodName.RpcUpdateStates, this.DirtyFlag, newValues);
				}
			} else {
				GD.PrintS(SynchronizedStateServer.NetId, "[NetworkNode] Sending states to authority.");
				this.RpcId(this.GetMultiplayerAuthority(), MethodName.RpcUpdateStates, this.DirtyFlag, newValues);
			}
		}
		this.DirtyFlag = 0;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcUpdateStates(uint dirtyFlags, Godot.Collections.Array values)
	{
		GD.PrintS(SynchronizedStateServer.NetId, "[NetworkNode] Received state update. Sender:", this.Multiplayer?.GetRemoteSenderId(), "Values:", values);
		foreach (
			(ReactiveVariant observable, int index)
			in this.Observables.Where((_, index) => (dirtyFlags & (1u << index)) != 0)
				.Select((observable, index) => (observable, index))
		) {
			this.IsUpdatingStates = true;
			GD.PrintS(SynchronizedStateServer.NetId, "[NetworkNode] Update performed.");
			observable.VariantValue = values[index];
			this.IsUpdatingStates = false;
		}
	}
}
