using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

// This should be a node you attach to a node that you want to synchronize over the network.
// This node looks for fields with the [Synchronized] attribute in it's parent node and synchronizes them using the
// same logic as MultiplayerSynchronized and NetworkSpawnableNode.
// The parent of this node must implement a IMultiplayerSynchronized interface that provides a MultiplayerSynchronized
// instance as a property so that it's easy to perform network operations on the node. (e.g. `Despawn()`) The
// MultiplayerSynchronized node could regiter itself as the MultiplayerSynchronized instance of it's parent in its
// _EnterTree hook so that it's available at _Ready time.
public partial class NetworkSynchronizer : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	// public const

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	private List<ReactiveVariant> SynchronizedVariables = new();
	private uint DirtyFlag = 0;
	private bool IsUpdatingStates = false;
	private Node ParentCache = null!;

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


	// -----------------------------------------------------------------------------------------------------------------
	// EVENTS
	// -----------------------------------------------------------------------------------------------------------------

	public override void _EnterTree()
	{
		base._EnterTree();
		this.ParentCache = this.GetParent();
		this.ParentCache.GetType()
			.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Where(field => field.GetCustomAttribute(typeof(SynchronizedAttribute)) != null)
			.Select(field => {
				if (field.GetValue(this.ParentCache) is not ReactiveVariant state) {
					GD.PushError($"[{nameof(NetworkSynchronizer)}] Failed to synchronize field {field.Name}. Cause: Field is not of type {nameof(ReactiveVariant)}.");
					return null;
				}
				return state;
			})
			.WhereNotNull()
			.ForEach(this.RegisterVariable);
		NetworkManager.Instance.RegisterSynchronizer(this);
		NetworkManager.Instance.PeerSceneChanged += OnPeerSceneChanged;
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		NetworkManager.Instance.PeerSceneChanged -= OnPeerSceneChanged;
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

	private void RegisterVariable(ReactiveVariant state)
	{
		int stateIndex = this.SynchronizedVariables.Count;
		state.Changed += () => {
			if (!this.IsUpdatingStates) {
				this.MarkStateDirty(stateIndex);
			}
		};
		this.SynchronizedVariables.Add(state);
	}

	private void OnPeerSceneChanged(ConnectedPeer peer)
	{
		if (
			!peer.IsLocalPeer
			&& this.IsMultiplayerAuthority()
			&& NetworkManager.Instance.LocalPeer?.IsInSameScene(peer) == true
			// Spawned nodes can't be synchronized at scene-change time because they are not in the scene tree yet. They
			// will be synchronized when they are spawned. (user should call Update() manually at _NetworkSpawned())
			&& !this.ParentCache.IsInGroup(NetworkManager.SPAWNED_GROUP)
		) {
			this.RpcId(peer.Id, MethodName.RpcSetValues, uint.MaxValue, this.GetLocalValues(uint.MaxValue));
		}
	}

	private void MarkStateDirty(int index) => this.MarkStatesDirty(1u << index);

	private void MarkStatesDirty(uint bitmask)
	{
		if (this.DirtyFlag == 0) {
			Callable.From(this.BroadcastValues).CallDeferred();
		}
		this.DirtyFlag |= bitmask;
	}

	private void MarkAllStatesDirty() => this.MarkStatesDirty(uint.MaxValue);

	public void ForceBroadcastSynchronizedFields() => this.MarkAllStatesDirty();

	private void BroadcastValues()
	{
		if (
			this.DirtyFlag == 0
			|| NetworkManager.Instance.ConnectionState.Value
				== NetworkManager.ConnectionStateEnum.Offline
		) {
			return;
		}
		Godot.Collections.Array newValues = new(
			this.SynchronizedVariables
				.Where((_, index) => (this.DirtyFlag & (1u << index)) != 0)
				.Select(observable => observable.VariantValue)
		);
		if (newValues.Count != 0) {
			if (this.IsMultiplayerAuthority()) {
				foreach (long peerId in NetworkManager.Instance.RemotePeersInScene.Select(peer => peer.Id)) {
					this.RpcId(peerId, MethodName.RpcSetValues, this.DirtyFlag, newValues);
				}
			} else {
				this.RpcId(this.GetMultiplayerAuthority(), MethodName.RpcSetValues, this.DirtyFlag, newValues);
			}
		} else {
		}
		this.DirtyFlag = 0;
	}

	public async Task Update()
	{
		if (this.IsMultiplayerAuthority()) {
			return;
		}
		Variant values = await NetworkManager.Instance.BiDiRpcId(
			this.GetMultiplayerAuthority(),
			this,
			MethodName.GetLocalValues,
			uint.MaxValue
		);
		this.SetLocalValues(uint.MaxValue, values.AsGodotArray());
	}

    public Godot.Collections.Array GetLocalValues(uint bitmask)
		=> new(
			this.SynchronizedVariables
				.Where((_, index) => (bitmask & (1u << index)) != 0)
				.Select(observable => observable.VariantValue)
		);

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcSetValues(uint bitmask, Godot.Collections.Array values) => this.SetLocalValues(bitmask, values);

    public void SetLocalValues(uint bitmask, Godot.Collections.Array values)
    {
		this.IsUpdatingStates = true;
		this.SynchronizedVariables
			.Where((_, index) => (bitmask & (1u << index)) != 0)
			.ForEach((reactVar, index) => reactVar.VariantValue = values[index]);
		this.IsUpdatingStates = false;
    }
}
