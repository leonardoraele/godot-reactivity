using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Raele.GodotReactivity;

public class ReactiveList<T> : Observable<Collection<T>>, ICollection<T>, IReadOnlyCollection<T>, IList<T>, IReadOnlyList<T>
{
	private ObservableCollection<T> _collection = new();

    public override Collection<T> Value {
        get {
            this.NotifyUsed();
            return this._collection;
        }
        set {
            this._collection = new(value);
            this.NotifyChanged();
        }
    }

    public ReactiveList()
	{
		this._collection.CollectionChanged += (_sender, _args) => this.NotifyChanged();
	}

    public ReactiveList(Collection<T> initialValue) : this() => this.Value = initialValue;
    public ReactiveList(IEnumerable<T> initialValue) : this(new(initialValue.ToList())) {}

    public static implicit operator Collection<T>(ReactiveList<T> list) => new(list);

    // -----------------------------------------------------------------------------------------------------------------
	// INTERFACE IMPLEMENTATIONS
    // -----------------------------------------------------------------------------------------------------------------

    public int Count {
        get {
            this.NotifyUsed();
            return this._collection.Count;
        }
    }
    public bool IsReadOnly => throw new System.NotImplementedException();

    public T this[int index] {
        get {
            this.NotifyUsed();
            return this._collection[index];
        }
        set => this._collection[index] = value;
    }

    public void Add(T item) => this._collection.Add(item);
    public void Clear() => this._collection.Clear();
    public bool Contains(T item)
    {
        this.NotifyUsed();
        return this._collection.Contains(item);
    }
    public void CopyTo(T[] array, int arrayIndex) => this._collection.CopyTo(array, arrayIndex);
    public bool Remove(T item) => this._collection.Remove(item);
    public void RemoveAt(int index) => this._collection.RemoveAt(index);
    public int IndexOf(T item) => this._collection.IndexOf(item);
    public void Insert(int index, T item) => this._collection.Insert(index, item);
    public IEnumerator<T> GetEnumerator()
    {
        this.NotifyUsed();
        return this._collection.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        this.NotifyUsed();
        return this._collection.GetEnumerator();
    }
}
