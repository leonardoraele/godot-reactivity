using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

/// <summary>
/// The NetworkSynchronizer node synchronizes the values of its parent's fields between network peers. To synchronize a
/// field, mark it with the [Synchronized] attribute. Whenever the field changes value, it is automatically synchronized
/// with all peers in the same scene. Synchronization only works if the authority for the node is in the scene, and for
/// as long as they remain in the scene. Only fields of type ReactiveVariant and its derived types can be synchronized.
/// (i.e. ReactiveVariant<T>, ReactiveVariantList, ReactiveChildrenList, ReactiveVariantCompatible<T>, etc.)
///
/// For nodes that are part of the main scene, you only need to attach this node as a child of the node with the
/// [Synchroniozed] fields and synchronization will work, no additional steps needed. For nodes that are instantiated at
/// runtime, you need to call the NetworkManager.Spawner.Spawn() method to instantiate the node to all peers together,
/// otherwise synchronization won't work.
/// </summary>
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
	private bool InOfflineMode = false;
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
		this.FindAndRegisterParentVariables();
		NetworkManager.Spawner.RegisterSynchronizer(this, this.ParentCache);
		NetworkManager.Scenes.PeerChangedScene += OnPeerChangedScene;
	}

    public override void _ExitTree()
	{
		base._ExitTree();
		NetworkManager.Scenes.PeerChangedScene -= OnPeerChangedScene;
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

    private void FindAndRegisterParentVariables()
    {
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
		this.ParentCache.GetType()
			.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Where(prop => prop.GetCustomAttribute(typeof(SynchronizedAttribute)) != null)
			.Select(prop => {
				if (prop.GetValue(this.ParentCache) is not ReactiveVariant state) {
					GD.PushError($"[{nameof(NetworkSynchronizer)}] Failed to synchronize property {prop.Name}. Cause: Property is not of type {nameof(ReactiveVariant)}.");
					return null;
				}
				return state;
			})
			.WhereNotNull()
			.ForEach(this.RegisterVariable);
    }

	private void RegisterVariable(ReactiveVariant state)
	{
		int stateIndex = this.SynchronizedVariables.Count;
		state.Changed += () => {
			if (!this.InOfflineMode) {
				this.MarkStateDirty(stateIndex);
			}
		};
		this.SynchronizedVariables.Add(state);
	}

	private void OnPeerChangedScene(ConnectedPeer peer)
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

	public void ForceBroadcastSynchronizedFields() => this.MarkAllStatesDirty();
	private void MarkAllStatesDirty() => this.MarkStatesDirty(uint.MaxValue);
	private void MarkStateDirty(int index) => this.MarkStatesDirty(1u << index);
	private void MarkStatesDirty(uint bitmask)
	{
		if (this.DirtyFlag == 0) {
			Callable.From(this.BroadcastChangedValues).CallDeferred();
		}
		this.DirtyFlag |= bitmask;
	}

	private void BroadcastChangedValues()
	{
		if (
			this.DirtyFlag == 0
			|| NetworkManager.Connectivity.Status.Value
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
				NetworkManager.RpcUtil.RpcOtherPeersInScene(this, MethodName.RpcSetValues, this.DirtyFlag, newValues);
			} else {
				NetworkManager.RpcUtil.RpcAuthorityInScene(this, MethodName.RpcSetValues, this.DirtyFlag, newValues);
			}
		}
		this.DirtyFlag = 0;
	}

	public async Task Update()
	{
		if (this.IsMultiplayerAuthority()) {
			return;
		}
		Variant values = await NetworkManager.RpcUtil.BiDiRpcId(
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

    private void SetLocalValues(uint bitmask, Godot.Collections.Array values)
    {
		using (this.OfflineMode()) {
			this.SynchronizedVariables
				.Where((_, index) => (bitmask & (1u << index)) != 0)
				.ForEach((reactVar, index) => reactVar.VariantValue = values[index]);
		}
    }

	public IDisposable OfflineMode()
	{
		this.InOfflineMode = true;
		return Disposable.Create(() => this.InOfflineMode = false);
	}
}
