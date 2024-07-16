using System;
using System.Collections.Generic;
using Godot;

namespace Raele.GodotReactivity.ExtensionMethods;

public static class ExtensionMethods
{
	public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
	{
		foreach (T item in source) {
			action(item);
		}
	}

	public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
	{
		int i = 0;
		foreach (T item in source) {
			action(item, i++);
		}
	}

	public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
	{
		foreach (T? item in source) {
			if (item != null) {
				yield return item;
			}
		}
	}

	public static NodePath GetParentPath(this NodePath path)
	{
		if (path.GetNameCount() == 0) {
			throw new ArgumentException("Cannot get parent path of an empty path.");
		} else if (path.GetNameCount() == 1) {
			if (path.IsAbsolute()) {
				throw new ArgumentException("Cannot get parent path of an absolute root path.");
			}
			switch (path.GetName(0)) {
				case ".": return "..";
				case "..": return "../..";
				default: return ".";
			}
		}
		string[] names = path.GetConcatenatedNames().Split("/");
		return (path.IsAbsolute() ? "/" : "") + string.Join("/", names[..^1]);
	}

	public static string GetNodeName(this NodePath path)
		=> path.GetName(path.GetNameCount() - 1);

	public static IEnumerable<R> SelectWhereValid<T, R>(this IEnumerable<T> source, Func<T, R> predicate, Func<R, bool>? validator = null)
	{
		validator ??= r => r != null;
		foreach (T item in source) {
			R result = predicate(item);
			if (validator(result)) {
				yield return result;
			}
		}
	}

	public static IEnumerable<Node> GetAncestors(this Node node)
	{
		Node? current = node.GetParent();
		while (current != null) {
			yield return current;
			current = current.GetParent();
		}
	}

	public static void RemoveAndDeleteAllChildren(this Node node)
	{
		while (node.GetChildCount() > 0) {
			Node child = node.GetChild(node.GetChildCount() - 1);
			node.RemoveChild(child);
			child.QueueFree();
		}
	}
}
