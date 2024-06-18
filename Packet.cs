using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text;

namespace BetterTogetherCore
{
    [MemoryPackable]
    public partial struct Packet
    {
        public PacketType Type { get; set; } = PacketType.None;
        public string Target { get; set; } = "";
        public string Key { get; set; } = "";
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