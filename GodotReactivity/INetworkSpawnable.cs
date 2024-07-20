using System;
using Godot;

namespace Raele.GodotReactivity;

public interface INetworkSpawnable
{
	public Guid NetId { get; }
	public string Name { get; set; }
	public void Initialize(Guid netId);
	public void _NetworkSpawned(Variant[] args);
}
