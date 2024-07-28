using System;

namespace Raele.GodotReactivity;

[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Event)]
public class SynchronizedAttribute : Attribute {}
