using System;

namespace Raele.GodotReactivity;

[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Event)]
public class SynchronizedAttribute : Attribute {
	/// <summary>
	/// By default, a synchronized field can only be updated by the authority. Of the synchronized node. If this is set
	/// to true, then the field can be updated by any peer. If this is false, updating the peer without being the
	/// authority will generate a warning, and the update will not be synchronized with the other peers.
	/// </summary>
	public bool Public { get; init; } = false; // TODO Implement this
}
