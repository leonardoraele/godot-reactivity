// using System;
// using Godot;

// namespace Raele.GodotReactivity;

// public interface INetworkSpawnable
// {
// 	// public NetId NetId { get; }
// 	public NetworkSynchronizer? Synchronizer { get; set; }
// 	// public void _NetworkReady(Variant[] args) {}
// 	public virtual void _NetworkSpawned(Variant[] args) {}
// }

// public class NetId
// {
// 	private Guid _guid = Guid.Empty;
// 	public Guid AsGuid {
// 		get => this._guid;
// 		set {
// 			if (!this._guid.Equals(Guid.Empty)) {
// 				throw new InvalidOperationException("NetId cannot be changed after being set.");
// 			}
// 			this._guid = value;
// 		}
// 	}
// 	public byte[] AsBytes {
// 		get => this._guid.ToByteArray();
// 		set => this.AsGuid = new Guid(value);
// 	}
// 	public Variant AsVariant {
// 		get => Variant.From(this.AsBytes);
// 		set => this.AsBytes = value.AsByteArray();
// 	}
//     public override string ToString() => this.AsGuid.ToString();

// 	public static implicit operator Guid(NetId netId) => netId.AsGuid;
// 	public static implicit operator Variant(NetId netId) => netId.AsVariant;
// 	public static implicit operator NetId(Variant variant) => new NetId { AsBytes = variant.AsByteArray() };
// }
