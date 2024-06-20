using System.Collections.ObjectModel;

namespace Raele.GodotReactivity;

public class ReactiveList<T> : Observable<ObservableCollection<T>>
{
	private ObservableCollection<T> _collection = new();

	public override ObservableCollection<T> ReadUntracked() => this._collection;

	public ReactiveList()
	{
		this._collection.CollectionChanged += (_sender, _args) => this.NotifyChanged();
	}
}
