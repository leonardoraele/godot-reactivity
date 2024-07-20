using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Raele.GodotReactivity;

public partial class NetworkManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------


	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	private Dictionary<int, TaskCompletionSource<Variant>> DataRequests = new();
	private int lastDataRequestId = 0;

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
	// BI-DIRECTIONAL RPC METHODS
	// -----------------------------------------------------------------------------------------------------------------

	public Task BiDiRpc(Node node, StringName method, params Variant[] args)
		=> Task.WhenAll(this.PeersInScene.Select(peer => this.BiDiRpcId(peer.Id, node, method, args)));

	public Task BiDiRpc(NodePath path, StringName method, params Variant[] args)
		=> Task.WhenAll(this.PeersInScene.Select(peer => this.BiDiRpcId(peer.Id, path, method, args)));

	public async Task<Variant> BiDiRpcId(long peerId, Node node, StringName method, params Variant[] args)
		=> await this.BiDiRpcId(peerId, node.GetPath(), method, args);

	public async Task<Variant> BiDiRpcId(long peerId, NodePath path, StringName method, params Variant[] args)
	{
		int id = ++lastDataRequestId;
		TaskCompletionSource<Variant> source = this.DataRequests[id] = new();
		this.RpcId(peerId, MethodName.RpcHandleBidiRpcCall, id, path, method, new Godot.Collections.Array(args));
		try {
			using (CancellationTokenSource canceler = new(5000)) {
				return await source.Task.WaitAsync(canceler.Token);
			}
		} finally {
			this.DataRequests.Remove(id);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleBidiRpcCall(int id, NodePath path, StringName method, Godot.Collections.Array args)
	{
		try {
			Variant result = this.GetNode(path).Call(method, args);
			this.RpcId(this.Multiplayer.GetRemoteSenderId(), MethodName.RpcHandleBiDiRpcResult, id, result);
		} catch (Exception e) {
			this.RpcId(this.Multiplayer.GetRemoteSenderId(), MethodName.RpcHandleBiDiRpcFailure, id, e.ToString());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleBiDiRpcResult(int id, Variant result)
	{
		if (this.DataRequests.TryGetValue(id, out TaskCompletionSource<Variant>? source)) {
			source.SetResult(result);
			this.DataRequests.Remove(id);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleBiDiRpcFailure(int id, string message)
	{
		if (this.DataRequests.TryGetValue(id, out TaskCompletionSource<Variant>? source)) {
			source.SetException(new Exception(message));
			this.DataRequests.Remove(id);
		}
	}
}
