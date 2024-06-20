using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text;

namespace BetterTogetherCore
{
    [MemoryPackable]
    public partial struct Packet
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
        public byte[] Data { get; set; } = new byte[0];

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
        /// Deserializes the data of the packet to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the expected object</typeparam>
        /// <returns></returns>
        public T? GetData<T>()
        {
            return MemoryPackSerializer.Deserialize<T>(Data);
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

    /// <summary>
    /// The mode of an RPC
    /// </summary>
    public enum RpcMode
    {
        /// <summary>
        /// The RPC is sent to a specific peer
        /// </summary>
        Target,
        /// <summary>
        /// The RPC is sent to all peers except the sender
        /// </summary>
        Others,
        /// <summary>
        /// The RPC is sent to all peers including the sender
        /// </summary>
        All,
        /// <summary>
        /// The RPC is sent to the server then back. Why would you use this? Feel free to enlighten me.
        /// </summary>
        Host,
        /// <summary>
        /// The RPC is sent to the server
        /// </summary>
        Server
    }
}