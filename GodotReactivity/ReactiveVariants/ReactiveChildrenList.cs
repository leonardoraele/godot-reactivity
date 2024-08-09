using System.Collections;
using System.Collections.Generic;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

public class ReactiveChildrenList
	: Observable<Godot.Collections.Array<Node>>,
	ICollection<Node>,
	IReadOnlyCollection<Node>,
	IList<Node>,
	IReadOnlyList<Node>
{
	private Node _parent;

	public ReactiveChildrenList(Node parent) {
		this._parent = parent;
		this._parent.ChildEnteredTree += this._NotifyChanged;
		this._parent.ChildExitingTree += this._NotifyChanged;
		this._parent.ChildOrderChanged += this.NotifyChanged;
	}

	~ReactiveChildrenList()
	{
		this._parent.ChildEnteredTree -= this._NotifyChanged;
		this._parent.ChildExitingTree -= this._NotifyChanged;
		this._parent.ChildOrderChanged -= this.NotifyChanged;
	}

	private void _NotifyChanged(Node _node) => this.NotifyChanged();

	public override Godot.Collections.Array<Node> Value {
		get {
			this.NotifyUsed();
			return this._parent.GetChildren();
		}
		set {
			this._parent.RemoveAndDeleteAllChildren();
			value.ForEach(this.Add);
		}
	}

	public int Count {
		get {
			this.NotifyUsed();
			return this._parent.GetChildCount();
		}
	}
	public bool IsReadOnly => throw new System.NotImplementedException();
	public Node this[int index] {
		get {
			this.NotifyUsed();
			return this._parent.GetChild(index);
		}
		set {
			this.RemoveAt(index);
			this.Insert(index, value);
		}
	}
    public void Add(Node item) => this._parent.AddChild(item);
	public void Clear() => this._parent.RemoveAndDeleteAllChildren();
	public bool Contains(Node item) => this.Value.Contains(item);
	public void CopyTo(Node[] array, int arrayIndex)
	{
		this.NotifyUsed();
		this.Value.CopyTo(array, arrayIndex);
	}
	public bool Remove(Node item)
	{
		if (this.Value.Contains(item)) {
			this._parent.RemoveChild(item);
			return true;
		}
		return false;
	}
	public IEnumerator<Node> GetEnumerator() => this.Value.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	public int IndexOf(Node item) => this.Value.IndexOf(item);
	public void Insert(int index, Node item)
	{
		this._parent.AddChild(item);
		this._parent.MoveChild(item, index);
	}
	public void RemoveAt(int index) => this._parent.RemoveChild(this._parent.GetChild(index));
}
