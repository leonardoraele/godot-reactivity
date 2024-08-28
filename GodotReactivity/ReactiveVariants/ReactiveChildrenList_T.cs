using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

public class ReactiveChildrenList<T>
	: Observable<IEnumerable<T?>>,
	ICollection<T?>,
	IReadOnlyCollection<T?>,
	IList<T?>,
	IReadOnlyList<T?>
	where T : Node
{
	public Node _parent;

	public ReactiveChildrenList(Node parent)
	{
		this._parent = parent;
		this._parent.ChildEnteredTree += this.OnChildrenListChanged;
		this._parent.ChildExitingTree += this.OnChildrenListChanged;
		this._parent.ChildOrderChanged += this.NotifyChanged;
	}

	~ReactiveChildrenList()
	{
		if (!this._parent.IsInstanceValid()) {
			return;
		}
		this._parent.ChildEnteredTree -= this.OnChildrenListChanged;
		this._parent.ChildExitingTree -= this.OnChildrenListChanged;
		this._parent.ChildOrderChanged -= this.NotifyChanged;
	}

	private void OnChildrenListChanged(Node _child) => this.NotifyChanged();

	public override IEnumerable<T?> Value {
		get {
			this.NotifyUsed();
			return this._parent.GetChildren().Select(node => node as T);
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
	public T? this[int index] {
		get {
			this.NotifyUsed();
			return this._parent.GetChild(index) as T;
		}
		set {
			this.RemoveAt(index);
			this.Insert(index, value);
		}
	}

	public void Add(T? item) => this._parent.AddChild(item);
	public void Clear() => this._parent.RemoveAndDeleteAllChildren();
	public bool Contains(T? item) => this.Value.Contains(item);
	public void CopyTo(T?[] array, int arrayIndex)
	{
		this.NotifyUsed();
		this.Value.ToArray().CopyTo(array, arrayIndex);
	}
	public bool Remove(T? item)
	{
		if (this.Value.Contains(item)) {
			this._parent.RemoveChild(item);
			return true;
		}
		return false;
	}
	public IEnumerator<T?> GetEnumerator() => this.Value.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => this.Value.GetEnumerator();
	public int IndexOf(T? item) => this.Value.ToList().IndexOf(item);
	public void Insert(int index, T? item)
	{
		this._parent.AddChild(item);
		this._parent.MoveChild(item, index);
	}
	public void RemoveAt(int index) => this._parent.RemoveChild(this._parent.GetChild(index));
}
