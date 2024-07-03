using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Raele.GodotReactivity;

public class ReactiveList<T> : Observable<ObservableCollection<T>>, ICollection<T>, IReadOnlyCollection<T>
{
	private ObservableCollection<T> _collection = new();

    public int Count => this._collection.Count;

    public bool IsReadOnly => throw new System.NotImplementedException();

    public override ObservableCollection<T> ReadUntracked() => this._collection;

    public ReactiveList()
	{
		this._collection.CollectionChanged += (_sender, _args) => this.NotifyChanged();
	}

	// ICollection<T> implementation
    public void Add(T item) => this._collection.Add(item);
    public void Clear() => this._collection.Clear();
    public bool Contains(T item) => this._collection.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => this._collection.CopyTo(array, arrayIndex);
    public bool Remove(T item) => this._collection.Remove(item);
    public IEnumerator<T> GetEnumerator() => this._collection.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this._collection.GetEnumerator();
}
