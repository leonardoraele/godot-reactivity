using System;
using Godot;

namespace Raele.GodotReactivity;

public static class ComputedState
{
	public static ComputedState<T> CreateInContext<T>(Node bind, Func<T> func)
		=> ComputedState<T>.CreateInContext(bind, func);
}
