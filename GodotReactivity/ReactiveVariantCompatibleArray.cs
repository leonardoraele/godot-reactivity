using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Raele.GodotReactivity;

public class ReactiveVariantCompatibleArray<T>
    : ReactiveVariantCompatible<ReactiveVariantCompatibleArray<T>.VariantConvertibleList<T>>,
	ICollection<T>,
	IReadOnlyCollection<T>,
	IList<T>,
	IReadOnlyList<T>
	where T : IVariantConverter<T>
{
	public class VariantConvertibleList<LT>
		: List<LT>, IVariantConverter<VariantConvertibleList<LT>>
		where LT : IVariantConverter<LT>
	{
		public VariantConvertibleList() : base() {}
		public VariantConvertibleList(IEnumerable<LT> initialValues) : base(initialValues) {}

		public static VariantConvertibleList<LT> FromVariant(Variant vArray) => new(vArray.AsGodotArray().Select(LT.FromVariant));
		public static Variant ToVariant(VariantConvertibleList<LT> tList) => new Godot.Collections.Array(tList.Select(LT.ToVariant));
	}

    public ReactiveVariantCompatibleArray() : base(new()) {}
	public ReactiveVariantCompatibleArray(IEnumerable<T> initialValues) : base(new(initialValues)) {}

	public static implicit operator List<T>(ReactiveVariantCompatibleArray<T> reactive) => reactive.Value;
	// public static implicit operator Godot.Collections.Array(ReactiveVariantCompatibleArray<T> reactive) => new(reactive.Value.Select(T.ToVariant));

	public T this[int index] {
		get {
			this.ThrowIfOutOfBounds(index);
			this.NotifyUsed();
			return this._value[index];
		}
		set {
			this.ThrowIfOutOfBounds(index);
			if (value == null ? this._value[index] == null : value.Equals(this._value[index])) {
				return;
			}
			this._value[index] = value;
			this.NotifyChanged();
		}
	}

	private void ThrowIfOutOfBounds(int index)
	{
		if (index < 0 || index >= Value.Count)
		{
			throw new IndexOutOfRangeException();
		}
	}

	public int Count {
		get {
			this.NotifyUsed();
			return this._value.Count;
		}
	}

	public bool IsReadOnly => throw new NotImplementedException();

	public void Add(T item)
	{
		this._value.Add(item);
		this.NotifyChanged();
	}

	public void Clear()
	{
		this._value.Clear();
		this.NotifyChanged();
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
		this._value.Insert(index, item);
		this.NotifyChanged();
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
		this.ThrowIfOutOfBounds(index);
		this._value.RemoveAt(index);
		this.NotifyChanged();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		this.NotifyUsed();
		return this._value.GetEnumerator();
	}
}
