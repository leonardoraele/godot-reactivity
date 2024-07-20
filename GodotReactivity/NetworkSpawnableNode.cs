using System;
using Godot;

namespace Raele.GodotReactivity;

public partial class NetworkSpawnableNode : NetworkNode
{
    private Guid _netId = Guid.Empty;

	public Guid NetId {
		get => this._netId;
		set {
			if (!this._netId.Equals(Guid.Empty)) {
				throw new InvalidOperationException("NetId cannot be changed after being set.");
			}
			this._netId = value;
		}
	}

	public Variant NetIdVariant {
		get => Variant.From(this._netId.ToByteArray());
		set => this.NetId = new Guid(value.AsByteArray());
	}

	/// <summary>
	/// This method is called by NetworkManager when this node is "network-ready". Which means all connected peers have
	/// already confirmed they have spawned their own versions of this node, so it's safe to send Rpc calls.
	///
	/// Calling this method will trigger _NetworkReady() to be called again, and synchronized states to be broadcast to
	/// peers again. (though you should use <see cref="ForceBroadcastSynchronizedStates"/> for that)
	/// </summary>
	public void NotifyNetworkReady(Variant[] args)
	{
		if (this.IsMultiplayerAuthority()) {
			this.ForceBroadcastSynchronizedStates();
		}
		this._NetworkReady(args);
	}

	protected virtual void _NetworkReady(Variant[] args) {}

	public void Despawn()
	{
		// TODO
	}
}
