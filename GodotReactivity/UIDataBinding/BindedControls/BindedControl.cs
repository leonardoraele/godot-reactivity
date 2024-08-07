using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Raele.GodotReactivity.BindedControls;

public abstract partial class BindedControl : Control
{
	private List<Observable> _states = new();
	private IEnumerator<Observable> _use_state = null!;

	public override void _Ready()
	{
		ReactiveEffect.CreateInContext(this, this.Render);
	}

	private void PrepareToRender() => this._use_state = this._states.GetEnumerator();

	private ReactiveState<T> UseState<T>(T initialValue)
	{
		if (this._use_state.MoveNext()) {
			if (this._use_state.Current is not ReactiveState<T> state) {
				throw new Exception($"Expected state of type {typeof(ReactiveState<T>)} but got {this._use_state.Current}");
			}
			return state;
		}
		return new(initialValue);
	}

	private void Render()
	{
		this.PrepareToRender();
		FormattableString fs = this._Render();
		List<object?> args = new(fs.GetArguments());
        string xmlStr = string.Format(fs.Format, args.Select((_arg, index) => $"\"{index}\""));

        Godot.XmlParser parserA = new();
		parserA.OpenBuffer(Encoding.ASCII.GetBytes(xmlStr));
        System.Xml.XmlDocument parserB = new();
		parserB.LoadXml(xmlStr);

		// var ast = parser.Read();
		// this.Hydrate(ast, args); // Traverses the tree comparing nodes and updating the UI as needed
	}

	public abstract FormattableString _Render();

	// Example:
	// public FormattableString _Render()
	// {
	// 	ReactiveState<LineEdit> lineEdit = new(null!); // or `var lineEdit = this.UseState<LineEdit>();`
	// 	ReactiveState<int> intValue = new(0); // or `var intValue = this.UseState(0);`
	// 	return $"""
	// 		<Button text="-" @pressed={() => intValue.Value--} />
	// 		<LineEdit ref={lineEdit} :text={intValue} @text_changed={() => intValue.Value = Int32.Parse(lineEdit.Text)} />
	// 		<Button text="+" @pressed={() => intValue.Value++} />
	// 	""";
	// }
}
