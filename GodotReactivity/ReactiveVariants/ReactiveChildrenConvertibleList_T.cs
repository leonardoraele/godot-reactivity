using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity;

public class ReactiveChildrenConvertibleList<N, V>
	: Observable<IEnumerable<V?>>,
	ICollection<V?>,
	IReadOnlyCollection<V?>,
	IList<V?>,
	IReadOnlyList<V?>
	where N : Node
{
	public Node _parent;
	private CrudActions _crud;

	public record CrudActions
	{
		public required Func<N> Create { get; init; }
		public required Func<N, V> Read { get; init; }
		public required Action<N, V> Update { get; init; }
		public required Action<N> Delete { get; init; }
	}

	public ReactiveChildrenConvertibleList(Node parent, CrudActions crud)
	{
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

	public override IEnumerable<V?> Value {
		get {
			this.NotifyUsed();
			foreach (Node node in this._parent.GetChildren()) {
				if (node is N nNode) {
					yield return this._crud.Read(nNode);
				} else {
					yield return default;
				}
			}
		}
		set {
			List<V?> valueList = value.ToList();
			valueList.ForEach((v, index) => this[index] = v);
			while (this.Count > valueList.Count) {
				this.RemoveAt(this.Count - 1);
			}
			this.NotifyChanged();
		}
	}
	public int Count {
		get {
			this.NotifyUsed();
			return this._parent.GetChildCount();
		}
	}
	public bool IsReadOnly => throw new NotImplementedException();
	public V? this[int index] {
		get {
			this.NotifyUsed();
			return this._parent.GetChild(index) is N nNode
				? this._crud.Read(nNode)
				: default;
		}
		set {
			if (this._parent.GetChild(index) is N nNode) {
				#pragma warning disable CS8604 // Possible null reference argument.
				this._crud.Update(nNode, value);
				#pragma warning restore CS8604 // Possible null reference argument.
			}
			this.NotifyChanged();
		}
	}
	public void Add(V? item)
	{
		N nNode = this._crud.Create();
		#pragma warning disable CS8604 // Possible null reference argument.
		this._crud.Update(nNode, item);
		#pragma warning restore CS8604 // Possible null reference argument.
		this._parent.AddChild(nNode);
	}
	public void Clear() => this._parent.RemoveAndDeleteAllChildren();
	public bool Contains(V? item) {
		this.NotifyUsed();
		return this.IndexOf(item) != -1;
	}
	// public bool Contains(V? item) => this._parent.GetChildren().Any(node => node is N nNode && this._crud.Read(nNode)?.Equals(item) == true);
	public void CopyTo(V?[] array, int arrayIndex) {
		this.NotifyUsed();
		this.Value.ToArray().CopyTo(array, arrayIndex);
	}
	public bool Remove(V? item)
	{
		int index = this.IndexOf(item);
		if (index != -1) {
			this.RemoveAt(index);
			return true;
		}
		return false;
	}
	public IEnumerator<V?> GetEnumerator()
	{
		this.NotifyUsed();
		return this.Value.GetEnumerator();
	}
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	public int IndexOf(V? item)
	{
		this.NotifyUsed();
		for (int i = 0; i < this.Count; i++) {
            V? current = this[i];
			if (current == null && item == null || current?.Equals(item) == true) {
				return i;
			}
		}
		return -1;
	}
	public void Insert(int index, V? item)
	{
		this.Add(item);
		this._parent.MoveChild(this._parent.GetChild(this._parent.GetChildCount() - 1), index);
	}
	public void RemoveAt(int index) => this._parent.RemoveChild(this._parent.GetChild(index));
}
