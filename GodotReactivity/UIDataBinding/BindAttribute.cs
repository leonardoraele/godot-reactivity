using System;
using System.Linq;
using Godot;

namespace Raele.GodotReactivity;

[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
public class BindAttribute : Attribute
{
	public readonly NodePath Path;
    public readonly StringName[] Signals;

    public BindAttribute(string path)
		// Can't use collection expression (aka. `[]`) in the second argument because type `string` is an enumeration of
		// `char`, so it would be ambiguous for the compiler what constructor to call.
		: this(path, new string[0]) {}
	public BindAttribute(string path, string signal)
		: this(path, [signal]) {}
	public BindAttribute(string path, string[] signals)
	{
		this.Path = path;
		this.Signals = signals.Select<string, StringName>(signal => signal).ToArray();
	}
}
