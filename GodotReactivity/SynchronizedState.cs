using System.Linq;
using Godot;

namespace Raele.GodotReactivity;

[GlobalClass][Tool] // Need to have [Tool] for _GetPropertyList()
public partial class SynchronizedState : SynchronizedNode
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public static SynchronizedState FromVariant(Variant variant) => new() { Value = variant };

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	private Variant.Type _dataType;
	[Export] public Variant.Type DataType {
		get => this._dataType;
		set {
			this._dataType = value;
			this.NotifyPropertyListChanged();
		}
	}

	public override Variant Value {
		get => this.SharedState.Value;
		set {
			this.SharedState.Value = value;
			this.DataType = value.VariantType;
		}
	}

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------

    public ReactiveVariant SharedState { get; private set; } = new();
    private ReactiveEffect SynchronizationEffect = null!;

	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------

	// ...

	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------

	// [Signal] public delegate void EventHandler()

	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------

	// private enum Type {
	// 	Value1,
	// }

	// -----------------------------------------------------------------------------------------------------------------
	// EVENTS
	// -----------------------------------------------------------------------------------------------------------------

    public override void _EnterTree()
    {
        base._EnterTree();
		if (Engine.IsEditorHint()) {
			this.SetProcess(false);
			return;
		}
		bool isInConstructor = true;
		this.SynchronizationEffect = ReactiveEffect.CreateInContext(this, () => {
			// Manually ensuring the dependency is added because, if the condition is not satisfied,
			// SharedState.Value won't be referenced, and the effect will never rerun.
			this.SharedState.NotifyUsed();
			if (!isInConstructor) {
				this.Rpc(MethodName.RpcSetValue, this.SharedState.Value);
			}
		});
		isInConstructor = false;
    }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        return new(
			(base._GetPropertyList() ?? new())
				.Append(new() {
					{ "name", PropertyName.Value },
					{ "type", (int) this.DataType },
					{ "usage", (int) PropertyUsageFlags.Default },
				})
		);
    }

	// -----------------------------------------------------------------------------------------------------------------
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	protected override void RpcSetValue(Variant newValue)
	{
		this.SynchronizationEffect.Enabled = false;
		base.RpcSetValue(newValue);
		this.SynchronizationEffect.Enabled = true;
	}
}
