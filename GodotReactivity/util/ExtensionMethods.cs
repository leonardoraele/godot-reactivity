using System;
using Godot;

namespace Raele.GodotReactivity.ExtensionMethods;

public static class ExtensionMethods
{
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
}
