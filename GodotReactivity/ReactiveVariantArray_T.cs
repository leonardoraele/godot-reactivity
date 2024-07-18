using System.Collections;
using System.Collections.Generic;
using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariantArray<[MustBeVariant] T>
	: ReactiveVariant<Godot.Collections.Array<T>>,
	IList<T>,
	ICollection<T>,
	IReadOnlyList<T>,
	IReadOnlyCollection<T>
{
    public ReactiveVariantArray() : base(new()) {}
    public ReactiveVariantArray(IEnumerable<T> values) : base(new(values)) {}

    public T this[int index] {
		get {
            this.ThrowIfIndexIsOutOfRange(index);
			this.NotifyUsed();
			return this._value[index];
		}
		set {
			if (value == null ? this._value[index] == null : value.Equals(this._value[index])) {
				return;
			}
			this.NotifyChanged();
			this._value[index] = value;
		}
	}

	private void ThrowIfIndexIsOutOfRange(int index, int allowMargin = 0)
	{
		if (index < 0 || index >= this._value.Count + allowMargin) {
			throw new System.ArgumentOutOfRangeException();
		}
	}

    public int Count {
		get {
			this.NotifyUsed();
			return this._value.Count;
		}
	}

	public bool IsReadOnly => throw new System.NotImplementedException();

	public void Add(T item)
	{
		this.NotifyChanged();
		this._value.Add(item);
	}

	public void Clear()
	{
		this.NotifyChanged();
		this._value.Clear();
	}

	public bool Contains(T item)
	{
		this.NotifyUsed();
		return this._value.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		this.NotifyUsed();
		this._value.CopyTo(array, arrayIndex);
	}

	public IEnumerator<T> GetEnumerator()
	{
		this.NotifyUsed();
		return this._value.GetEnumerator();
	}

	public int IndexOf(T item)
	{
		this.NotifyUsed();
		return this._value.IndexOf(item);
	}

	public void Insert(int index, T item)
	{
		this.ThrowIfIndexIsOutOfRange(index, 1);
		this.NotifyChanged();
		this._value.Insert(index, item);
	}

	public bool Remove(T item)
	{
        if (this._value.Remove(item)) {
            this.NotifyChanged();
            return true;
        }
        return false;
	}

	public void RemoveAt(int index)
	{
		this.ThrowIfIndexIsOutOfRange(index);
        this.NotifyChanged();
        this._value.RemoveAt(index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		this.NotifyUsed();
		return this._value.GetEnumerator();
	}
}
