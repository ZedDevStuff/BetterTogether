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

        public Packet() { }

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
    public enum PacketType
    {
        None,
        SetState,
        Init,
        Ping,
        RPC,
        SelfConnected,
        PeerConnected,
        PeerDisconnected
    }
}