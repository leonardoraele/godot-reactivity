using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using Raele.PocketWars;
using Raele.PocketWars.ExtensionMethods;

namespace Raele.GodotReactivity;

[GlobalClass]
public partial class SynchronizedDictionary : SynchronizedNode, IDictionary<string, SynchronizedNode>
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public static SynchronizedDictionary FromDictionary(Godot.Collections.Dictionary dict) => new() { Value = dict };
	public static SynchronizedDictionary FromDictionary(Godot.Collections.Dictionary<string, Variant> dict) => new() { Value = dict };
	public static SynchronizedDictionary FromObject(GodotObject obj) => SynchronizedDictionary.FromDictionary(
		new Godot.Collections.Dictionary<string, Variant>(
			new Dictionary<string, Variant>(
				obj.GetPropertyList()
					.Where(prop =>
						(prop["usage"].AsInt64() & (long) PropertyUsageFlags.ScriptVariable) != 0
					)
					.Select(prop => prop["name"].AsString())
					.Select(propName => new KeyValuePair<string, Variant>(propName, obj.Get(propName)))
			)
		)
	);

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public bool PeersCanEdit { get; set; } = false; // TODO

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	private ReactiveList<string> KeysCache = new();

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	public override Variant Value {
		get => this.AsDictionary();
		set {
			// TODO Don't need to remove all children, just the ones that are not in the new dictionary
			this.Clear();
			Godot.Collections.Dictionary dict = value.AsGodotDictionary();
			dict.Keys.ForEach(key => this[key.AsString()] = SynchronizedNode.From(dict[key]));
		}
	}

    // -----------------------------------------------------------------------------------------------------------------
    // SIGNALS
    // -----------------------------------------------------------------------------------------------------------------

    // [Signal] public delegate void EventHandler()

    // -----------------------------------------------------------------------------------------------------------------
    // INTERNAL TYPES
    // -----------------------------------------------------------------------------------------------------------------

    // private enum Type {
    // 	Value1,
    // }

    // -----------------------------------------------------------------------------------------------------------------
    // EVENTS
    // -----------------------------------------------------------------------------------------------------------------

    public override void _EnterTree()
    {
        base._EnterTree();
		this.ChildEnteredTree += this.OnChildEnteredTree;
		this.ChildExitingTree += this.OnChildExitingTree;
    }

    // -----------------------------------------------------------------------------------------------------------------
    // METHODS
    // -----------------------------------------------------------------------------------------------------------------

	public void OnChildEnteredTree(Node child)
	{
		if (child is not SynchronizedNode synchronizedNode) {
			GD.PushWarning($"Node {child} does not implement {nameof(SynchronizedNode)}. Removing...");
			child.QueueFree();
			return;
		}
		this.KeysCache.Add(child.Name);
	}
	public void OnChildExitingTree(Node child) => this.KeysCache.Remove(child.Name);

    public Godot.Collections.Dictionary AsSimpleDictionary()
	{
		Godot.Collections.Dictionary dict = new();
		this.Keys.ForEach(key => dict[key] = this.GetValue(key));
		return dict;
	}
	public Godot.Collections.Dictionary<string, Variant> AsDictionary() => new(
		new Dictionary<string, Variant>(
			this.Keys.Select(key => new KeyValuePair<string, Variant>(key, this[key].Value))
		)
	);

	public void SetValue(string key, Variant value) => this.Add(key, SynchronizedNode.From(value));
	public Variant GetValue(string key) => this[key].Value;

	// -----------------------------------------------------------------------------------------------------------------
	// COLLECTION & LIST INTERFACE IMPLEMENTATIONS
	// -----------------------------------------------------------------------------------------------------------------

	public ICollection<string> Keys => this.KeysCache.ToList();
	public ICollection<SynchronizedNode> Values => this.Keys.Select(key => this[key]).ToList();
	public int Count => this.KeysCache.Count;
	public bool IsReadOnly => throw new NotImplementedException();
	public SynchronizedNode this[string key] {
		get => (this.GetNode(key) as SynchronizedNode)!;
		set {
			if (this.HasNode(key)) {
				this[key].Value = value.Value;
			} else {
				this.Add(key, value);
			}
		}
	}
	public void Add(string key, SynchronizedNode node)
	{
		if (this.KeysCache.Contains(key)) {
			this[key] = node;
		} else {
			node.Name = key;
			this.AddChild(node);
		}
	}
	public bool ContainsKey(string key) => this.KeysCache.Contains(key);
	public bool Remove(string key)
	{
		if (this.HasNode(key)) {
			Node node = this.GetNode(key);
			this.RemoveChild(node);
			node.QueueFree();
			return true;
		}
		return false;
	}
	public bool TryGetValue(string key, [MaybeNullWhen(false)] out SynchronizedNode value)
	{
		value = this.HasNode(key) ? this[key] : null;
		return value != null;
	}
	public void Add(KeyValuePair<string, SynchronizedNode> item) => this.Add(item.Key, item.Value);
	public void Clear() => this.Keys.ForEach(key => this.Remove(key));
	public bool Contains(KeyValuePair<string, SynchronizedNode> item) => this.ContainsKey(item.Key);
	public void CopyTo(KeyValuePair<string, SynchronizedNode>[] array, int arrayIndex)
	{
		string[] keys = this.Keys.ToArray();
		for (int i = 0; i < keys.Length; i++) {
			array[arrayIndex + i] = new KeyValuePair<string, SynchronizedNode>(keys[i], this[keys[i]]);
		}
	}
	public bool Remove(KeyValuePair<string, SynchronizedNode> item)
	{
		if (item.Value.GetParent() == this) {
			this.RemoveChild(item.Value);
			item.Value.QueueFree();
			return true;
		}
		return false;
	}
	public IEnumerator<KeyValuePair<string, SynchronizedNode>> GetEnumerator() => this.Keys.Select(key => new KeyValuePair<string, SynchronizedNode>(key, this[key])).GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
