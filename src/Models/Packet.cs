using MemoryPack;
using System;

namespace BetterTogetherCore.Models
{
    [MemoryPackable]
    public partial class Packet
    {
        /// <summary>
        /// The type of the packet
        /// </summary>
        public PacketType Type { get; set; } = PacketType.None;
        /// <summary>
        /// The target of the packet. This can be an id or a name like "server"
        /// </summary>
        public string Target { get; set; } = "";
        /// <summary>
        /// The key is used differently depending on the packet type. For example, in a SetState packet, the key is the state name.
        /// </summary>
        public string Key { get; set; } = "";
        /// <summary>
        /// The data of the packet. This can be anything Memorypack can handle.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Empty constructor
        /// </summary>
        public Packet() { }

        /// <summary>
        /// Constructor for a packet
        /// </summary>
        /// <param name="type">The packet type</param>
        /// <param name="target">The target of the packet</param>
        /// <param name="key">The key of the packet</param>
        /// <param name="data">The Memorypacked object to send</param>
        [MemoryPackConstructor]
        public Packet(PacketType type, string target, string key, byte[] data)
        {
            Type = type;
            Target = target;
            Key = key;
            Data = data;
        }
        /// <summary>
        /// Create a new packet with the specified data type. MemoryPack can't serialize <c>object</c> so generics are used.
         /// </summary>
        /// <param name="type">The packet type</param>
        /// <param name="target">The target of the packet</param>
        /// <param name="key">The key of the packet</param>
        /// <param name="data">The object to send. Must be Memorypackable</param>
        public static Packet New<T>(PacketType type, string target, string key, T data)
        {
            return new Packet(type, target, key, MemoryPackSerializer.Serialize(data));
        }
        /// <summary>
        /// Deserializes the data of the packet to the specified type
        /// </summary>
        /// <typeparam name="T">The type of the expected object</typeparam>
        /// <returns>The deserialized object or <c>null</c></returns>
        public T? GetData<T>()
        {
            if(Data.Length == 0) return default(T?); // Return null if the data is empty (no data to deserialize
            return MemoryPackSerializer.Deserialize<T>(Data);
        }
        /// <summary>
        /// Sets the data of the packet to the specified object
        /// </summary>
        /// <typeparam name="T"><c>MemoryPackable</c> object</typeparam>
        /// <param name="data">The object to serialize. The object must be Memorypackable.</param>
        public void SetData<T>(T data)
        {
            Data = MemoryPackSerializer.Serialize(data);
        }

        /// <summary>
        /// Serializes the packet
        /// </summary>
        /// <returns>The serialized packet</returns>
        public byte[] Pack()
        {
            return MemoryPackSerializer.Serialize(this);
        }
    }
    /// <summary>
    /// Various types of packets
    /// </summary>
    public enum PacketType
    {
        /// <summary>
        /// Default value. Doesn't mean anything.
        /// </summary>
        None,
        /// <summary>
        /// Used to set the state of a peer
        /// </summary>
        SetState,
        /// <summary>
        /// Used to delete the state of a peer
        /// </summary>
        DeleteState,
        /// <summary>
        /// Sent to the peer with the current state and other data
        /// </summary>
        Init,
        /// <summary>
        /// Used for all ping related stuff
        /// </summary>
        Ping,
        /// <summary>
        /// Used for RPC calls
        /// </summary>
        RPC,
        /// <summary>
        /// Sent to the connected peer when the connection is established
        /// </summary>
        SelfConnected,
        /// <summary>
        /// Sent to all peers when a peer connects
        /// </summary>
        PeerConnected,
        /// <summary>
        /// Sent to all peers when a peer disconnects
        /// </summary>
        PeerDisconnected,
        /// <summary>
        /// Sent to kicked peers
        /// </summary>
        Kick,
        /// <summary>
        /// Sent to banned peers
        /// </summary>
        Ban
    }
}