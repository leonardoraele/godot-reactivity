using System.Linq;
using System.Reflection;
using Godot;
using Raele.GodotReactivity.ExtensionMethods;

namespace Raele.GodotReactivity.UIDataBinding;

public partial class UIDataBindingManager : Node
{
	// -----------------------------------------------------------------------------------------------------------------
	// STATICS
	// -----------------------------------------------------------------------------------------------------------------

	public const string GROUP_UI_CONTROLLER = "ui_controller";

	// -----------------------------------------------------------------------------------------------------------------
	// EXPORTS
	// -----------------------------------------------------------------------------------------------------------------

	// [Export] public

	// -----------------------------------------------------------------------------------------------------------------
	// FIELDS
	// -----------------------------------------------------------------------------------------------------------------



	// -----------------------------------------------------------------------------------------------------------------
	// PROPERTIES
	// -----------------------------------------------------------------------------------------------------------------



	// -----------------------------------------------------------------------------------------------------------------
	// SIGNALS
	// -----------------------------------------------------------------------------------------------------------------

	// [Signal] public delegate void EventHandler()

	// -----------------------------------------------------------------------------------------------------------------
	// INTERNAL TYPES
	// -----------------------------------------------------------------------------------------------------------------

	private record UIDataBinding(Node Owner, BindAttribute Attr, ReactiveVariant ReactiveVar) {
		public Node Target => this.Owner.GetNode(this.Attr.Path);
	}

	// -----------------------------------------------------------------------------------------------------------------
	// EVENTS
	// -----------------------------------------------------------------------------------------------------------------

	public override void _EnterTree()
	{
		base._EnterTree();
		this.GetTree().NodeAdded += this.OnNodeAdded;
	}

	// public override void _Ready()
	// {
	// 	base._Ready();
	// }

	// public override void _Process(double delta)
	// {
	// 	base._Process(delta);
	// }

	// public override void _PhysicsProcess(double delta)
	// {
	// 	base._PhysicsProcess(delta);
	// }

	// public override string[] _GetConfigurationWarnings()
	// 	=> base._PhysicsProcess(delta);

	// -----------------------------------------------------------------------------------------------------------------
	// METHODS
	// -----------------------------------------------------------------------------------------------------------------

	private void OnNodeAdded(Node node)
	{
		if (node.IsInGroup(GROUP_UI_CONTROLLER)) {
			Callable.From(() => this.RegisterBindings(node)).CallDeferred();
		}
	}

	private void RegisterBindings(Node node)
	{
		node.GetType()
			.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Select(field =>
				field.GetCustomAttribute<BindAttribute>() is BindAttribute attr
					&& field.GetValue(node) is ReactiveVariant reactiveVar
					? new UIDataBinding(node, attr, reactiveVar)
					: null
			)
			.WhereNotNull()
			.ForEach(this.RegisterBinding);
		node.GetType()
			.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Select(prop =>
				prop.GetCustomAttribute<BindAttribute>() is BindAttribute attr
					&& prop.GetValue(node) is ReactiveVariant reactiveVar
					? new UIDataBinding(node, attr, reactiveVar)
					: null
			)
			.WhereNotNull()
			.ForEach(this.RegisterBinding);
	}

	private void RegisterBinding(UIDataBinding binding)
	{
        ReactiveEffect effect = ReactiveEffect.CreateInContext(
			binding.Owner,
			() => binding.Owner.SetIndexed(binding.Attr.Path, binding.ReactiveVar.VariantValue)
		);
		Callable updateReactiveVar = Callable.From(() => {
			using (effect.DisabledContext()) {
				binding.ReactiveVar.VariantValue = binding.Owner.GetIndexed(binding.Attr.Path);
			}
		});
		binding.Attr.Signals.ForEach(signal => {
			binding.Target.Connect(signal, updateReactiveVar);
			binding.Owner.TreeExiting += () => binding.Target.Disconnect(signal, updateReactiveVar);
		});
	}
}
