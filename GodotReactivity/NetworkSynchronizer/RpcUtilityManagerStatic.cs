using System.Threading.Tasks;
using Godot;

namespace Raele.GodotReactivity;

public static class RpcUtilityManagerStatic
{
	public static Task<Variant> BiDiRpcId(this Node node, long peerId, StringName method, params Variant[] args)
		=> NetworkManager.RpcUtil.BiDiRpcId(peerId, node, method, args);
	public static void SafeRpcToEveryone(this Node target, StringName methodName, params Variant[] args)
		=> NetworkManager.RpcUtil.SafeRpcToEveryone(target, methodName, args);

	public static void SafeRpcToOthers(this Node target, StringName methodName, params Variant[] args)
		=> NetworkManager.RpcUtil.SafeRpcToOthers(target, methodName, args);

	public static void SafeRpcToAuthority(this Node target, StringName methodName, params Variant[] args)
		=> NetworkManager.RpcUtil.SafeRpcToAuthority(target, methodName, args);
}
