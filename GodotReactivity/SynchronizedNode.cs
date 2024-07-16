using Godot;

namespace Raele.GodotReactivity;

public abstract partial class SynchronizedNode : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public static SynchronizedNode From(Variant variant)
		=> variant.VariantType switch {
			Variant.Type.Array => SynchronizedList.FromArray(variant.AsGodotArray()),
			Variant.Type.Dictionary => SynchronizedDictionary.FromDictionary(variant.AsGodotDictionary()),
			Variant.Type.Object => SynchronizedDictionary.FromObject(variant.AsGodotObject()),
			_ => SynchronizedState.FromVariant(variant),
		};
	public static Variant GetValue(Node node)
		=> node is SynchronizedNode synchronizedNode
			? synchronizedNode.Value
			: throw new System.InvalidOperationException($"Node {node} does not implement {nameof(SynchronizedNode)}");
	public static bool TryGetValue(Node node, out Variant variant)
	{
		if (node is SynchronizedNode synchronizedNode) {
			variant = synchronizedNode.Value;
			return true;
		} else {
			variant = new Variant();
			return false;
		}
	}
	public static void SetValue(Node node, Variant variant)
	{
		if (node is SynchronizedNode synchronizedNode) {
			synchronizedNode.Value = variant;
		} else {
			GD.PushWarning($"Node {node} does not implement {nameof(SynchronizedNode)}");
		}
	}

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public bool PeersCanWrite { get; set; } = false; // TODO

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	public abstract Variant Value { get; set; }

	// -----------------------------------------------------------------------------------------------------------------
	// GODOT EVENTS
	// -----------------------------------------------------------------------------------------------------------------

	public override void _Ready()
	{
		base._Ready();
		// if (this.GetParent() is not SynchronizedNode) {
			if (this.IsMultiplayerAuthority()) {
				SynchronizedStateServer.Instance.BroadcastState(this);
			} else {
				this.RpcId(this.GetMultiplayerAuthority(), MethodName.RpcRequestValueUpdate);
			}
		// }
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (this.IsMultiplayerAuthority()) {
			this.Rpc(MethodName.RpcDelete);
		}
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		this.Multiplayer.Poll();
	}

	// -----------------------------------------------------------------------------------------------------------------
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	public SynchronizedList AsSynchronizedList()
	{
		if (this is not SynchronizedList list) {
			throw new System.InvalidOperationException($"Node {this} does not implement {nameof(SynchronizedList)}");
		}
		return list;
	}

	public SynchronizedDictionary AsSynchronizedDictionary()
	{
		if (this is not SynchronizedDictionary dict) {
			throw new System.InvalidOperationException($"Node {this} does not implement {nameof(SynchronizedDictionary)}");
		}
		return dict;
	}

	// -----------------------------------------------------------------------------------------------------------------
	// RPC METHODS
	// -----------------------------------------------------------------------------------------------------------------

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RpcRequestValueUpdate()
		=> this.RpcId(this.Multiplayer.GetRemoteSenderId(), MethodName.RpcSetValue, this.Value);
	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	protected virtual void RpcSetValue(Variant newValue) => this.Value = newValue;
	[Rpc(MultiplayerApi.RpcMode.Authority)]
	public void RpcDelete() => this.QueueFree();
}
