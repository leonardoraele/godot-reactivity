using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Raele.GodotReactivity;

[GlobalClass]
public partial class SynchronizedList : SynchronizedNode, ICollection<SynchronizedNode>, IReadOnlyCollection<SynchronizedNode>, IList<SynchronizedNode>, IReadOnlyList<SynchronizedNode>
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public static SynchronizedList FromArray(Godot.Collections.Array array) => [..array.Select(SynchronizedNode.From)];

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public bool PeersCanEdit { get; set; } = false; // TODO

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

	private ReactiveList<SynchronizedNode> ChildrenCache = new();

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	public override Variant Value {
		get => new Godot.Collections.Array(this.Values);
		set {
			Godot.Collections.Array array = value.AsGodotArray();
			for (int i = 0; i < array.Count; i++) {
				if (i < this.Count) {
					this[i].Value = array[i];
				} else {
					this.Add(SynchronizedNode.From(array[i]));
				}
			}
		}
	}
	public IEnumerable<Variant> Values => this.ChildrenCache.Select(synchedNode => synchedNode.Value);

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
		this.ChildOrderChanged += this.OnChildOrderChanged;
	}

	// -----------------------------------------------------------------------------------------------------------------
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	private void OnChildExitingTree(Node node)
		=> this.ChildrenCache.RemoveAt(node.GetIndex());
	private void OnChildEnteredTree(Node node)
	{
		if (node is not SynchronizedNode syncNode) {
			GD.PushWarning($"Node {node} is not a {nameof(SynchronizedNode)}. Removing from tree...");
			this.RemoveChild(node);
			node.QueueFree();
			return;
		}
		this.ChildrenCache.Insert(node.GetIndex(), syncNode);
	}
	private void OnChildOrderChanged()
	{
		// TODO This implementation will fire too many reruns of reactive effects
		this.ChildrenCache.Clear();
		foreach (Node node in this.GetChildren()) {
			if (node is not SynchronizedNode synchedNode) {
				throw new InvalidOperationException($"Node {node} is not a {nameof(SynchronizedNode)}");
			}
			this.ChildrenCache.Add(synchedNode);
		}
	}

	public void AddValue(Variant value) => this.Add(SynchronizedNode.From(value));
	public void ContainsValue(Variant value) => this.Values.Contains(value);
	public void RemoveValue(Variant value) => this.RemoveAt(this.IndexOfValue(value));
	public int IndexOfValue(Variant value) => this.Values.ToList().IndexOf(value);
	public int FindIndex(Func<Variant, bool> predicate)
	{
		for (int i = 0; i < this.Count; i++) {
			if (predicate(this[i].Value)) {
				return i;
			}
		}
		return -1;
	}
	public void InsertValue(int index, Variant value) => this.Insert(index, SynchronizedNode.From(value));

	// -----------------------------------------------------------------------------------------------------------------
	// COLLECTION & LIST INTERFACE IMPLEMENTATIONS
	// -----------------------------------------------------------------------------------------------------------------

	public int Count => this.ChildrenCache.Count;
	public bool IsReadOnly => throw new NotImplementedException();
	public SynchronizedNode this[int index] {
		get => this.ChildrenCache[index];
		set => throw new InvalidOperationException();
	}
	public void Add(SynchronizedNode item) => this.AddChild(item, forceReadableName: true);
	public void Clear()
	{
		while (this.Count > 0) {
			this.RemoveAt(this.Count - 1);
		}
	}
	public bool Contains(SynchronizedNode item) => item.GetParent() == this;
	public void CopyTo(SynchronizedNode[] array, int arrayIndex) => this.ToArray().CopyTo(array, arrayIndex);
	public bool Remove(SynchronizedNode item)
	{
		if (this.Contains(item)) {
			this.RemoveChild(item);
			item.QueueFree();
			return true;
		}
		return false;
	}
	public IEnumerator<SynchronizedNode> GetEnumerator() => this.ToArray().AsEnumerable().GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	public int IndexOf(SynchronizedNode item) => this.ChildrenCache.IndexOf(item);
	public void Insert(int index, SynchronizedNode item)
	{
		if (index < 0 || index > this.Count) {
			throw new ArgumentOutOfRangeException(nameof(index));
		}
		this.AddChild(item);
		this.MoveChild(item, index);
	}
	public void RemoveAt(int index) => this.RemoveChild(this.ChildrenCache[index]);
}
