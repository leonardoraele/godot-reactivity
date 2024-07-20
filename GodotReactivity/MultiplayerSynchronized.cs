// This should be a node you attach to a node that you want to synchronize over the network.
// This node looks for fields withe the [Synchronized] attribute in it's parent node and synchronizes them using the
// same logic as NetworkNode and NetworkSpawnableNode.
// The parent of this node must implement a IMultiplayerSynchronized interface that provides a MultiplayerSynchronized
// instance as a property so that it's easy to perform network operations on the node. (e.g. `Despawn()`) The
// MultiplayerSynchronized node could regiter itself as the MultiplayerSynchronized instance of it's parent in its
// _EnterTree hook so that it's available at _Ready time.
