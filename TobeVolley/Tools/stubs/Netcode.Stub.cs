// STUB (compile-check only): public API surface of Netcode for GameObjects 2.x (Unity.Netcode.Runtime),
// reduced to what the game uses. Signatures mirror v2.9.2. Not functional.
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Unity.Netcode
{
    // ---- RPCs ----
    public enum SendTo { Owner, NotOwner, Server, NotServer, Me, NotMe, Everyone, ClientsAndHost, Authority, NotAuthority, SpecifiedInParams }
    public enum RpcDelivery { Reliable = 0, Unreliable }
    public enum RpcInvokePermission { Everyone = 0, Server, Owner }
    public enum LocalDeferMode { Default, Defer, SendImmediate }
    public enum RpcTargetUse { Temp, Persistent }

    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public RpcDelivery Delivery = RpcDelivery.Reliable;
        public RpcInvokePermission InvokePermission;
        [Obsolete("RequireOwnership is deprecated. Please use InvokePermission = RpcInvokePermission.Owner or InvokePermission = RpcInvokePermission.Everyone instead.")]
        public bool RequireOwnership;
        public bool DeferLocal;
        public bool AllowTargetOverride;
        public RpcAttribute(SendTo target) { }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerRpcAttribute : RpcAttribute
    {
        [Obsolete("ServerRpc with RequireOwnership is deprecated. Use [Rpc(SendTo.Server, InvokePermission = ...)] instead.")]
        public new bool RequireOwnership;
        public ServerRpcAttribute() : base(SendTo.Server) { }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : RpcAttribute { public ClientRpcAttribute() : base(SendTo.NotServer) { } }

    public abstract class BaseRpcTarget { }
    public class RpcTarget
    {
        public BaseRpcTarget Owner, NotOwner, Server, NotServer, Me, NotMe, Everyone, ClientsAndHost, Authority, NotAuthority;
        public BaseRpcTarget Single(ulong clientId, RpcTargetUse use) => null;
        public BaseRpcTarget Not(ulong excludedClientId, RpcTargetUse use) => null;
        public BaseRpcTarget Group(ulong[] clientIds, RpcTargetUse use) => null;
        public BaseRpcTarget Group<T>(T clientIds, RpcTargetUse use) where T : IEnumerable<ulong> => null;
        public BaseRpcTarget Not(ulong[] excludedClientIds, RpcTargetUse use) => null;
        public BaseRpcTarget Not<T>(T excludedClientIds, RpcTargetUse use) where T : IEnumerable<ulong> => null;
    }
    public struct RpcSendParams
    {
        public BaseRpcTarget Target; public LocalDeferMode LocalDeferMode;
        public static implicit operator RpcSendParams(BaseRpcTarget target) => new RpcSendParams { Target = target };
        public static implicit operator RpcSendParams(LocalDeferMode m) => new RpcSendParams { LocalDeferMode = m };
    }
    public struct RpcReceiveParams { public ulong SenderClientId; }
    public struct RpcParams
    {
        public RpcSendParams Send; public RpcReceiveParams Receive;
        public static implicit operator RpcParams(RpcSendParams s) => new RpcParams { Send = s };
        public static implicit operator RpcParams(BaseRpcTarget t) => new RpcParams { Send = new RpcSendParams { Target = t } };
        public static implicit operator RpcParams(LocalDeferMode m) => new RpcParams { Send = new RpcSendParams { LocalDeferMode = m } };
        public static implicit operator RpcParams(RpcReceiveParams r) => new RpcParams { Receive = r };
    }
    public struct ServerRpcSendParams { }
    public struct ServerRpcReceiveParams { public ulong SenderClientId; }
    public struct ServerRpcParams { public ServerRpcSendParams Send; public ServerRpcReceiveParams Receive; }
    public struct ClientRpcSendParams { public IReadOnlyList<ulong> TargetClientIds; public NativeArray<ulong>? TargetClientIdsNativeArray; }
    public struct ClientRpcReceiveParams { }
    public struct ClientRpcParams { public ClientRpcSendParams Send; public ClientRpcReceiveParams Receive; }

    // ---- Serialization ----
    public interface INetworkSerializeByMemcpy { }
    public interface IReaderWriter { bool IsReader { get; } bool IsWriter { get; } }
    public interface INetworkSerializable { void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter; }
    public struct ForPrimitives { } public struct ForEnums { } public struct ForStructs { } public struct ForNetworkSerializable { } public struct ForFixedStrings { }
    public ref struct BufferSerializer<TReaderWriter> where TReaderWriter : IReaderWriter
    {
        public bool IsReader => true; public bool IsWriter => false;
        public void SerializeValue(ref string s, bool oneByteChars = false) { }
        public void SerializeValue<T>(ref T value, ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> { }
        public void SerializeValue<T>(ref T value, ForEnums unused = default) where T : unmanaged, Enum { }
        public void SerializeValue<T>(ref T value, ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy { }
        public void SerializeValue<T>(ref T value, ForNetworkSerializable unused = default) where T : INetworkSerializable, new() { }
        public void SerializeValue<T>(ref T value, ForFixedStrings unused = default) where T : unmanaged, INativeList<byte>, IUTF8Bytes { }
        public void SerializeValue<T>(ref T[] value, ForPrimitives unused = default) where T : unmanaged, IComparable, IConvertible, IComparable<T>, IEquatable<T> { }
        public void SerializeValue<T>(ref T[] value, ForEnums unused = default) where T : unmanaged, Enum { }
        public void SerializeValue<T>(ref T[] value, ForStructs unused = default) where T : unmanaged, INetworkSerializeByMemcpy { }
        public void SerializeValue<T>(ref T[] value, ForNetworkSerializable unused = default) where T : INetworkSerializable, new() { }
        public void SerializeValue(ref Vector2 v) { } public void SerializeValue(ref Vector3 v) { } public void SerializeValue(ref Vector4 v) { }
        public void SerializeValue(ref Quaternion v) { } public void SerializeValue(ref Color v) { } public void SerializeValue(ref Color32 v) { }
        public void SerializeValue(ref Ray v) { } public void SerializeValue(ref Ray2D v) { }
        public void SerializeNetworkSerializable<T>(ref T value) where T : INetworkSerializable, new() { }
        public bool PreCheck(int amount) => true;
        public void SerializeValuePreChecked(ref string s, bool oneByteChars = false) { }
    }

    // ---- NetworkVariable ----
    public enum NetworkVariableReadPermission { Everyone, Owner }
    public enum NetworkVariableWritePermission { Server, Owner }
    public abstract class NetworkVariableBase : IDisposable
    {
        public string Name { get; }
        public bool IsDirty() => false;
        public virtual void SetDirty(bool isDirty) { }
        public NetworkBehaviour GetBehaviour() => null;
        public NetworkVariableReadPermission ReadPerm { get; }
        public NetworkVariableWritePermission WritePerm { get; }
        public void Dispose() { }
    }
    public class NetworkVariable<T> : NetworkVariableBase
    {
        public delegate void OnValueChangedDelegate(T previousValue, T newValue);
        public OnValueChangedDelegate OnValueChanged;
        public NetworkVariable(T value = default, NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server) { }
        public virtual T Value { get; set; }
        public bool CheckDirtyState(bool isWrite = false) => false;
    }
    public class NetworkList<T> : NetworkVariableBase, IList<T> where T : unmanaged, IEquatable<T>
    {
        public NetworkList(IEnumerable<T> values = default, NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server) { }
        public delegate void OnListChangedDelegate(NetworkListEvent<T> changeEvent);
        public event OnListChangedDelegate OnListChanged;
        public int Count => 0; public bool IsReadOnly => false;
        public T this[int index] { get => default; set { } }
        public IEnumerator<T> GetEnumerator() => null; System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
        public void Add(T item) { } public void Clear() { } public bool Contains(T item) => false; public void CopyTo(T[] array, int arrayIndex) { }
        public bool Remove(T item) => false; public int IndexOf(T item) => 0; public void Insert(int index, T item) { } public void RemoveAt(int index) { }
    }
    public struct NetworkListEvent<T>
    {
        public enum EventType : byte { Add, Insert, Remove, RemoveAt, Value, Clear, Full }
        public EventType Type; public int Index; public T Value; public T PreviousValue;
    }

    // ---- Core ----
    public struct NetworkTime { public double Time => 0; public float TimeAsFloat => 0; public int Tick => 0; public double FixedDeltaTime => 0; public float FixedDeltaTimeAsFloat => 0; }
    public class NetworkClient { public ulong ClientId; public NetworkObject PlayerObject; public bool IsServer, IsClient, IsHost, IsConnected, IsApproved, IsSessionOwner; public List<NetworkObject> OwnedObjects; }
    public enum NetworkTopologyTypes { ClientServer, DistributedAuthority }
    public class NetworkPrefabs { public void Add(NetworkPrefab prefab) { } public bool Remove(GameObject prefab) => false; public bool Contains(GameObject prefab) => false; }
    public struct NetworkPrefab { public GameObject Prefab; }
    public abstract class NetworkTransport : MonoBehaviour { public const ulong ServerClientId = 0; }
    public class NetworkConfig
    {
        public ushort ProtocolVersion; public NetworkTransport NetworkTransport; public GameObject PlayerPrefab; public NetworkPrefabs Prefabs = new NetworkPrefabs();
        public uint TickRate = 30; public int ClientConnectionBufferTimeout = 10; public bool ConnectionApproval; public byte[] ConnectionData = new byte[0];
        public bool EnableTimeResync; public int TimeResyncInterval = 30; public bool EnsureNetworkVariableLengthSafety; public bool EnableSceneManagement = true;
        public bool ForceSamePrefabs = true; public bool RecycleNetworkIds = true; public float NetworkIdRecycleDelay = 120f; public int LoadSceneTimeOut = 120;
        public float SpawnTimeout = 10f; public bool EnableNetworkLogs = true; public NetworkTopologyTypes NetworkTopology; public bool UseCMBService; public bool AutoSpawnPlayerPrefabClientSide = true;
    }
    public struct ConnectionEventData { public ConnectionEvent EventType; public ulong ClientId; public ulong[] PeerClientIds; }
    public enum ConnectionEvent { ClientConnected, PeerConnected, ClientDisconnected, PeerDisconnected }
    public class NetworkSpawnManager { public Dictionary<ulong, NetworkObject> SpawnedObjects; public IReadOnlyList<NetworkObject> SpawnedObjectsList; public NetworkObject GetLocalPlayerObject() => null; public NetworkObject GetPlayerNetworkObject(ulong clientId) => null; }

    public class NetworkManager : MonoBehaviour
    {
        public const ulong ServerClientId = 0;
        public static NetworkManager Singleton { get; private set; }
        public static event Action<NetworkManager> OnInstantiated;
        public static event Action<NetworkManager> OnDestroying;
        public NetworkConfig NetworkConfig;
        public ulong LocalClientId { get; }
        public IReadOnlyDictionary<ulong, NetworkClient> ConnectedClients { get; }
        public IReadOnlyList<NetworkClient> ConnectedClientsList { get; }
        public IReadOnlyList<ulong> ConnectedClientsIds { get; }
        public NetworkClient LocalClient { get; }
        public bool IsServer { get; } public bool IsClient { get; } public bool IsHost { get; } public bool ServerIsHost { get; }
        public bool IsListening { get; } public bool IsConnectedClient { get; } public bool IsApproved { get; }
        public string DisconnectReason { get; }
        public NetworkTime LocalTime { get; } public NetworkTime ServerTime { get; }
        public NetworkSpawnManager SpawnManager { get; }
        public event Action<ulong> OnClientConnectedCallback;
        public event Action<ulong> OnClientDisconnectCallback;
        public event Action<NetworkManager, ConnectionEventData> OnConnectionEvent;
        public event Action OnServerStarted; public event Action OnClientStarted; public event Action OnTransportFailure; public event Action OnPreShutdown;
        public event Action<bool> OnServerStopped; public event Action<bool> OnClientStopped;
        public Action<ConnectionApprovalRequest, ConnectionApprovalResponse> ConnectionApprovalCallback;
        public bool StartServer() => false; public bool StartClient() => false; public bool StartHost() => false;
        public void Shutdown(bool discardMessageQueue = false) { }
        public void DisconnectClient(ulong clientId) { } public void DisconnectClient(ulong clientId, string reason = null) { }
        public void SetSingleton() { }
        public struct ConnectionApprovalRequest { public byte[] Payload; public ulong ClientNetworkId; }
        public class ConnectionApprovalResponse { public bool Approved; public bool CreatePlayerObject; public uint? PlayerPrefabHash; public Vector3? Position; public Quaternion? Rotation; public bool Pending; public string Reason; }
    }
    // Real signature lives in NetworkManager as nested types; keep top-level aliases out to avoid ambiguity.

    public class NetworkObject : MonoBehaviour
    {
        public ulong NetworkObjectId { get; } public ulong OwnerClientId { get; } public bool IsOwner { get; } public bool IsOwnedByServer { get; }
        public bool IsLocalPlayer { get; } public bool IsPlayerObject { get; } public bool IsSpawned { get; } public bool HasAuthority { get; } public bool IsSceneObject { get; }
        public bool AutoObjectParentSync = true; public bool SynchronizeTransform = true; public bool DontDestroyWithOwner; public bool ActiveSceneSynchronization; public bool SceneMigrationSynchronization = true; public bool SpawnWithObservers = true;
        public NetworkManager NetworkManager { get; }
        public bool IsNetworkVisibleTo(ulong clientId) => true;
        public void NetworkShow(ulong clientId) { } public void NetworkHide(ulong clientId) { }
        public static NetworkObject InstantiateAndSpawn(GameObject networkPrefab, NetworkManager networkManager, ulong ownerClientId = 0, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default) => null;
        public NetworkObject InstantiateAndSpawn(NetworkManager networkManager, ulong ownerClientId = 0, bool destroyWithScene = false, bool isPlayerObject = false, bool forceOverride = false, Vector3 position = default, Quaternion rotation = default) => null;
        public void Spawn(bool destroyWithScene = false) { }
        public void SpawnWithOwnership(ulong clientId, bool destroyWithScene = false) { }
        public void SpawnAsPlayerObject(ulong clientId, bool destroyWithScene = false) { }
        public void Despawn(bool destroy = true) { }
        public void RemoveOwnership() { }
        public void ChangeOwnership(ulong newOwnerClientId) { }
        public bool TrySetParent(Transform parent, bool worldPositionStays = true) => true;
        public bool TrySetParent(GameObject parent, bool worldPositionStays = true) => true;
        public bool TrySetParent(NetworkObject parent, bool worldPositionStays = true) => true;
        public bool TryRemoveParent(bool worldPositionStays = true) => true;
    }

    public abstract class NetworkBehaviour : MonoBehaviour
    {
        public RpcTarget RpcTarget { get; }
        public bool IsLocalPlayer { get; } public bool IsOwner { get; } public bool IsServer { get; } public bool HasAuthority { get; } public bool IsSessionOwner { get; }
        public bool ServerIsHost { get; } public bool IsClient { get; } public bool IsHost { get; } public bool IsOwnedByServer { get; } public bool IsSpawned { get; }
        public NetworkManager NetworkManager { get; }
        public NetworkObject NetworkObject { get; }
        public bool HasNetworkObject => true;
        public ulong NetworkObjectId { get; } public ushort NetworkBehaviourId { get; } public ulong OwnerClientId { get; }
        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }
        public virtual void OnNetworkPreDespawn() { }
        public virtual void OnGainedOwnership() { }
        public virtual void OnLostOwnership() { }
        public virtual void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject) { }
        public virtual void OnDestroy() { }
        public virtual void OnSynchronize<T>(ref BufferSerializer<T> serializer) where T : IReaderWriter { }
    }
}

namespace Unity.Netcode.Components
{
    using Unity.Netcode;
    public class NetworkTransform : NetworkBehaviour
    {
        public bool SyncPositionX = true, SyncPositionY = true, SyncPositionZ = true, SyncRotAngleX = true, SyncRotAngleY = true, SyncRotAngleZ = true, SyncScaleX = true, SyncScaleY = true, SyncScaleZ = true;
        public float PositionThreshold, RotAngleThreshold = 0.01f, ScaleThreshold = 0.01f;
        public bool InLocalSpace, Interpolate = true, UseQuaternionSynchronization, UseHalfFloatPrecision, SlerpPosition;
        protected virtual bool OnIsServerAuthoritative() => true;
        public void Teleport(Vector3 newPosition, Quaternion newRotation, Vector3 newScale) { }
        public void SetState(Vector3? posIn = null, Quaternion? rotIn = null, Vector3? scaleIn = null, bool shouldGhostsInterpolate = true) { }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { }
    }
    public class NetworkAnimator : NetworkBehaviour
    {
        public Animator Animator { get; set; }
        protected virtual bool OnIsServerAuthoritative() => true;
        public void SetTrigger(string triggerName) { } public void SetTrigger(int hash) { } public void ResetTrigger(string triggerName) { } public void ResetTrigger(int hash) { }
    }
}

namespace Unity.Netcode.Transports.UTP
{
    public class UnityTransport : NetworkTransport
    {
        public struct ConnectionAddressData { public string Address; public ushort Port; public string ServerListenAddress; }
        public ConnectionAddressData ConnectionData;
        public void SetConnectionData(string ipv4Address, ushort port, string listenAddress = null) { }
        public void SetRelayServerData(string ipv4Address, ushort port, byte[] allocationIdBytes, byte[] keyBytes, byte[] connectionDataBytes, byte[] hostConnectionDataBytes = null, bool isSecure = false) { }
        public int MaxPayloadSize;
        public int ConnectTimeoutMS, MaxConnectAttempts, DisconnectTimeoutMS, HeartbeatTimeoutMS;
    }
}
