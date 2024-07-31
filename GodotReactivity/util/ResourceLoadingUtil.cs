using System;
using System.Threading.Tasks;
using Godot;

namespace Raele.GodotReactivity;

public static class ResourceLoadingUtil
{
	public static async Task<PackedScene> LoadSceneAsync(string scenePath, Action<Variant>? updateProgress = null)
	{
		if (Engine.GetMainLoop() is not SceneTree tree) {
			throw new InvalidOperationException("Cannot load scene asynchronously if the main loop is not SceneTree.");
		}
		TaskCompletionSource<PackedScene> source = new();
		Godot.Collections.Array progressArray = new();
		ResourceLoader.LoadThreadedRequest(scenePath, nameof(PackedScene));
		void OnProcessFrame() {
			ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(scenePath, progressArray);
			Variant progress = progressArray[0];
			switch (status) {
				case ResourceLoader.ThreadLoadStatus.InProgress:
					updateProgress?.Invoke(progress);
					break;
				case ResourceLoader.ThreadLoadStatus.InvalidResource:
				case ResourceLoader.ThreadLoadStatus.Failed:
					string errorMessage = "Failed to load resource: " + status.ToString();
					source.SetException(new System.Exception(errorMessage));
					break;
				case ResourceLoader.ThreadLoadStatus.Loaded:
                    Resource resource = ResourceLoader.LoadThreadedGet(scenePath);
					source.SetResult((resource as PackedScene)!);
					break;
			}
		}
		tree.ProcessFrame += OnProcessFrame;
		try {
			return await source.Task;
		} finally {
			tree.ProcessFrame -= OnProcessFrame;
		}
	}
}
