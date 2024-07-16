using System.Collections;
using System.Collections.Generic;

namespace Raele.GodotReactivity;

public class ReactiveSet<T> : Observable<HashSet<T>>, ICollection<T>, IReadOnlyCollection<T>, ISet<T>, IReadOnlySet<T>
{
	private HashSet<T> _set = new();

    public override HashSet<T> Value {
        get {
            this.NotifyUsed();
            return this._set;
        }
        set {
            this._set = new(value);
            this.NotifyChanged();
        }
    }

    // -----------------------------------------------------------------------------------------------------------------
	// INTERFACE IMPLEMENTATIONS
    // -----------------------------------------------------------------------------------------------------------------

    public int Count {
        get {
            this.NotifyUsed();
            return this._set.Count;
        }
    }
    public bool IsReadOnly => throw new System.NotImplementedException();

    public void Add(T item)
    {
        this._set.Add(item);
        this.NotifyChanged();
    }

    public void Clear()
    {
        this._set.Clear();
        this.NotifyChanged();
    }

    public bool Contains(T item)
    {
        this.NotifyUsed();
        return this._set.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        this.NotifyUsed();
        this._set.CopyTo(array, arrayIndex);
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        this._set.ExceptWith(other);
        this.NotifyChanged();
    }

    public IEnumerator<T> GetEnumerator()
    {
        this.NotifyUsed();
        foreach (var item in this._set) {
            yield return item;
        }
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        this._set.IntersectWith(other);
        this.NotifyChanged();
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        this.NotifyUsed();
        return this._set.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        this.NotifyUsed();
        return this._set.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        this.NotifyUsed();
        return this._set.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        this.NotifyUsed();
        return this._set.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        this.NotifyUsed();
        return this._set.Overlaps(other);
    }

    public bool Remove(T item)
    {
        var result = this._set.Remove(item);
        if (result) {
            this.NotifyChanged();
        }
        return result;
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        this.NotifyUsed();
        return this._set.SetEquals(other);
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        this._set.SymmetricExceptWith(other);
        this.NotifyChanged();
    }

    public void UnionWith(IEnumerable<T> other)
    {
        this._set.UnionWith(other);
        this.NotifyChanged();
    }

    bool ISet<T>.Add(T item)
    {
        bool result = this._set.Add(item);
        if (result) {
            this.NotifyChanged();
        }
        return result;
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
