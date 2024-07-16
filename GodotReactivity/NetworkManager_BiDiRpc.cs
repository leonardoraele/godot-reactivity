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

	private Dictionary<int, TaskCompletionSource<Variant>> PendingBidiRpcCalls = new();
	private int lastBidiRpcCallId = 0;

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

	// public Task BiDiRpc(Node node, StringName method, params Variant[] args)
	// 	=> Task.WhenAll(this.PeersInScene.Select(peer => this.BiDiRpcId(peer.Id, node, method, args)));

	// public Task BiDiRpc(NodePath path, StringName method, params Variant[] args)
	// 	=> Task.WhenAll(this.PeersInScene.Select(peer => this.BiDiRpcId(peer.Id, path, method, args)));

	public async Task<Variant> BiDiRpcId(long peerId, Node node, StringName method, params Variant[] args)
		=> await this.BiDiRpcId(peerId, node.GetPath(), method, args);

	public async Task<Variant> BiDiRpcId(long peerId, NodePath path, StringName method, params Variant[] args)
	{
		int rpcCallId = ++lastBidiRpcCallId;
		TaskCompletionSource<Variant> source = this.PendingBidiRpcCalls[rpcCallId] = new();
		this.RpcId(peerId, MethodName.RpcHandleBidiRpcCall, rpcCallId, path, method, new Godot.Collections.Array(args));
		try {
			using (CancellationTokenSource canceler = new(5000)) {
				return await source.Task.WaitAsync(canceler.Token);
			}
		} finally {
			this.PendingBidiRpcCalls.Remove(rpcCallId);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleBidiRpcCall(int rpcCallId, NodePath path, StringName method, Godot.Collections.Array args)
	{
		try {
			Variant result = this.GetNode(path).Call(method, [..args]);
			this.RpcId(this.Multiplayer.GetRemoteSenderId(), MethodName.RpcHandleBiDiRpcResult, rpcCallId, result);
		} catch (Exception e) {
			this.RpcId(this.Multiplayer.GetRemoteSenderId(), MethodName.RpcHandleBiDiRpcFailure, rpcCallId, e.ToString());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleBiDiRpcResult(int id, Variant result)
	{
		if (this.PendingBidiRpcCalls.TryGetValue(id, out TaskCompletionSource<Variant>? source)) {
			source.SetResult(result);
			this.PendingBidiRpcCalls.Remove(id);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcHandleBiDiRpcFailure(int id, string message)
	{
		if (this.PendingBidiRpcCalls.TryGetValue(id, out TaskCompletionSource<Variant>? source)) {
			source.SetException(new Exception(message));
			this.PendingBidiRpcCalls.Remove(id);
		}
	}
}
