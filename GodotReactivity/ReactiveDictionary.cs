using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization.Formatters;

namespace Raele.GodotReactivity;

public class ReactiveDictionary<K, V>
	: Observable<Dictionary<K, V>>,
	ICollection<KeyValuePair<K, V>>,
	IReadOnlyCollection<KeyValuePair<K, V>>,
	IDictionary<K, V>,
	IReadOnlyDictionary<K, V>
	where K : notnull
{
	private Dictionary<K, V> _dict = new();

	public override Dictionary<K, V> Value {
		get {
			this.NotifyUsed();
			return this._dict;
		}
		set {
			this._dict = new(value);
			this.NotifyChanged();
		}
	}

	// -----------------------------------------------------------------------------------------------------------------
	// INTERFACE IMPLEMENTATIONS
	// -----------------------------------------------------------------------------------------------------------------

	public V this[K key] {
		get {
			this.NotifyUsed();
			return this._dict[key];
		}
		set {
			this._dict[key] = value;
			this.NotifyChanged();
		}
	}
	public int Count
	{
		get {
			this.NotifyUsed();
			return this._dict.Count;
		}
	}
	public bool IsReadOnly => throw new System.NotImplementedException();
	public ICollection<K> Keys
	{
		get {
			this.NotifyUsed();
			return this._dict.Keys;
		}
	}
	public ICollection<V> Values
	{
		get {
			this.NotifyUsed();
			return this._dict.Values;
		}
	}
	IEnumerable<K> IReadOnlyDictionary<K, V>.Keys => this.Keys;
	IEnumerable<V> IReadOnlyDictionary<K, V>.Values => this.Values;
	public void Add(KeyValuePair<K, V> item)
	{
		this._dict.Add(item.Key, item.Value);
		this.NotifyChanged();
	}
	public void Add(K key, V value)
	{
		this._dict.Add(key, value);
		this.NotifyChanged();
	}
	public void Clear()
	{
		this._dict.Clear();
		this.NotifyChanged();
	}
	public bool Contains(KeyValuePair<K, V> item)
	{
		this.NotifyUsed();
		return this._dict.Contains(item);
	}
	public bool ContainsKey(K key)
	{
		this.NotifyUsed();
		return this._dict.ContainsKey(key);
	}
	public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
	{
		this.NotifyUsed();
		this._dict.ToArray().CopyTo(array, arrayIndex);
	}
	public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
	{
		this.NotifyUsed();
		return this._dict.GetEnumerator();
	}
	public bool Remove(KeyValuePair<K, V> item)
	{
		if (
			this.TryGetValue(item.Key, out V? value)
			&& (value == null ? item.Value == null : value.Equals(item.Value))
			&& this._dict.Remove(item.Key)
		) {
			this.NotifyChanged();
			return true;
		}
		return false;
	}

	public bool Remove(K key)
	{
		if (this._dict.Remove(key)) {
			this.NotifyChanged();
			return true;
		}
		return false;
	}

	public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
	{
		this.NotifyUsed();
		return this._dict.TryGetValue(key, out value);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		this.NotifyUsed();
		return this._dict.GetEnumerator();
	}
}
