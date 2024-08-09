using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

public class ReactiveChildrenConvertibleList
	: Observable<Godot.Collections.Array<Variant>>,
	ICollection<Variant>,
	IReadOnlyCollection<Variant>,
	IList<Variant>,
	IReadOnlyList<Variant>
{
	private Node _parent;
	private CrudActions _crud;

	public record CrudActions
	{
		public required Func<Node> Create { get; init; }
		public required Func<Node, Variant> Read { get; init; }
		public required Action<Node, Variant> Update { get; init; }
		public required Action<Node> Delete { get; init; }
	}

	public ReactiveChildrenConvertibleList(
		Node parent,
		CrudActions crud
	) {
		this._parent = parent;
		this._crud = crud;
		this._parent.ChildEnteredTree += this.OnChildrenListChanged;
		this._parent.ChildExitingTree += this.OnChildrenListChanged;
		this._parent.ChildOrderChanged += this.NotifyChanged;
	}

	~ReactiveChildrenConvertibleList()
	{
		this._parent.ChildEnteredTree -= this.OnChildrenListChanged;
		this._parent.ChildExitingTree -= this.OnChildrenListChanged;
		this._parent.ChildOrderChanged -= this.NotifyChanged;
	}

	private void OnChildrenListChanged(Node _child) => this.NotifyChanged();

	public override Godot.Collections.Array<Variant> Value {
		get {
			this.NotifyUsed();
			return new(this._parent.GetChildren().Select(node => this._crud.Read(node)));
		}
		set {
			value.ForEach((item, index) => this[index] = item);
			while (this.Count > value.Count) {
				this.RemoveAt(this.Count - 1);
			}
			this.NotifyChanged();
		}
	}
	public int Count => this._parent.GetChildCount();
	public bool IsReadOnly => throw new NotImplementedException();
	public Variant this[int index] {
		get {
			this.NotifyUsed();
			return this._crud.Read(this._parent.GetChild(index));
		}
		set {
			this._crud.Update(this._parent.GetChild(index), value);
			this.NotifyChanged();
		}
	}
	public void Add(Variant item)
	{
		Node newNode = this._crud.Create();
		this._crud.Update(newNode, item);
		this._parent.AddChild(newNode);
	}
	public void Clear() => this._parent.RemoveAndDeleteAllChildren();
	public bool Contains(Variant item)
	{
		this.NotifyUsed();
		return this._parent.GetChildren().Any(node => this._crud.Read(node).Equals(item));
	}
	public void CopyTo(Variant[] array, int arrayIndex)
	{
		this.NotifyUsed();
		this.Value.ToArray().CopyTo(array, arrayIndex);
	}
	public bool Remove(Variant item)
	{
		int index = this.IndexOf(item);
		if (index != -1) {
			this.RemoveAt(index);
			return true;
		}
		return false;
	}
	public IEnumerator<Variant> GetEnumerator()
	{
		this.NotifyUsed();
		foreach (Node node in this._parent.GetChildren()) {
			yield return this._crud.Read(node);
		}
	}
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	public int IndexOf(Variant item)
	{
		this.NotifyUsed();
		return this._parent.GetChildren().Select(this._crud.Read).FindIndex(value => item.Equals(value));
	}
	public void Insert(int index, Variant item)
	{
		this.Add(item);
		this._parent.MoveChild(this._parent.GetChild(this._parent.GetChildCount() - 1), index);
	}
	public void RemoveAt(int index) => this._parent.RemoveChild(this._parent.GetChild(index));
}
