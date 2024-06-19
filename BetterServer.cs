using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using LiteNetLib;
using MemoryPack;

namespace BetterTogetherCore
{
    /// <summary>
    /// The BetterTogether server. Create one with a max player count then use the Start method to start the server on the specified port. Set the <c>DataReceived</c> <c>Func<![CDATA[<]]>NetPeer, Packet, Packet<![CDATA[>]]></c> for your data validation and handling.
    /// </summary>
    public class BetterServer
    {
        /// <summary>
        /// The max amount of players
        /// </summary>
        public int MaxPlayers { get; private set; }
        /// <summary>
        /// The underlying <c>LiteNetLib.NetManager</c>
        /// </summary>
        public NetManager? NetManager { get; private set; } = null;
        /// <summary>
        /// The underlying <c>LiteNetLib.EventBasedNetListener</c>
        /// </summary>
        public EventBasedNetListener Listener { get; private set; } = new EventBasedNetListener();
        private ConcurrentDictionary<string, byte[]> _States { get; set; } = new();
        /// <summary>
        /// Returns a read-only dictionary of the states on the server
        /// </summary>
        public ReadOnlyDictionary<string, byte[]> States => new ReadOnlyDictionary<string, byte[]>(_States);
        private ConcurrentDictionary<string, NetPeer> _Players { get; set; } = new();
        /// <summary>
        /// Returns a read-only dictionary of the players on the server
        /// </summary>
        public ReadOnlyDictionary<string, NetPeer> Players => new ReadOnlyDictionary<string, NetPeer>(_Players);

        /// <summary>
        /// Creates a new server with the specified max player count
        /// </summary>
        /// <param name="maxPlayers">The max amount of players</param>
        public BetterServer(int maxPlayers = 10)
        {
            MaxPlayers = maxPlayers;
            Listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }
        private void PollEvents()
        {
            while (NetManager != null)
            {
                NetManager.PollEvents();
                Thread.Sleep(15);
            }
        }
        /// <summary>
        /// Starts the server on the specified port
        /// </summary>
        /// <param name="port"></param>
        public void Start(int port)
        {
            NetManager = new NetManager(Listener);
            try
            {
                NetManager.Start(port);
            }
            catch (Exception ex)
            {
                return;
            }
            Thread thread = new Thread(PollEvents);
            thread.Start();
        }
        
        /// <summary>
        /// Stops the server
        /// </summary>
        public void Stop()
        {
            NetManager?.Stop();
            NetManager = null;
        }

        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {
            if(_Players.Count >= MaxPlayers)
            {
                string reason = "Server is full";
                request.Reject(Encoding.UTF8.GetBytes(reason));
                return;
            }
            request.AcceptIfKey("BetterTogether");
        }
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            string id = Guid.NewGuid().ToString();
            while(_Players.ContainsKey(id))
            {
                id = Guid.NewGuid().ToString();
            }
            List<string> players = [id,.._Players.Keys];
            Packet packet1 = new Packet(PacketType.PeerConnected, "", "Connected", Encoding.UTF8.GetBytes(id));
            byte[] bytes = MemoryPackSerializer.Serialize(packet1);
            foreach (var player in _Players)
            {
                if(player.Key != id)
                {
                    player.Value.Send(bytes, DeliveryMethod.ReliableOrdered);
                }
            }
            _Players[id] = peer;
            byte[] data = MemoryPackSerializer.Serialize(players);
            Packet packet2 = new Packet(PacketType.SelfConnected, "", "Connected", data);
            peer.Send(MemoryPackSerializer.Serialize(packet2), DeliveryMethod.ReliableOrdered);
            byte[] states = MemoryPackSerializer.Serialize(_States);
            Packet packet3 = new Packet(PacketType.Init, "", "Init", states);
            peer.Send(MemoryPackSerializer.Serialize(packet3), DeliveryMethod.ReliableOrdered);
        }
        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes > 0)
            {
                byte[] bytes = reader.GetRemainingBytes();
                Packet? packet = MemoryPackSerializer.Deserialize<Packet>(bytes);
                if (packet != null)
                {
                    if (DataReceived != null) packet = DataReceived(peer, packet.Value);
                    if (packet != null)
                    {
                        switch (packet.Value.Type)
                        {
                            case PacketType.Ping:
                                if(packet.Value.Target == "server") peer.Send(bytes, deliveryMethod);
                                else
                                {
                                    string origin = GetPeerId(peer);
                                    NetPeer? targetPeer = _Players.FirstOrDefault(x => x.Key == packet.Value.Target).Value;
                                    if (origin != null && targetPeer != null)
                                    {
                                        if(packet.Value.Key == "pong")
                                        {
                                            Packet pong = new Packet();
                                            pong.Type = PacketType.Ping;
                                            pong.Target = packet.Value.Target;
                                            targetPeer.Send(MemoryPackSerializer.Serialize(pong), deliveryMethod);
                                        }
                                        else
                                        {
                                            Packet ping = new Packet();
                                            ping.Type = PacketType.Ping;
                                            ping.Target = origin;
                                            targetPeer.Send(MemoryPackSerializer.Serialize(ping), deliveryMethod);
                                        }
                                    }
                                }
                                break;
                            case PacketType.SetState:
                                _States[packet.Value.Key] = packet.Value.Data;
                                SyncState(packet.Value, bytes, DeliveryMethod.ReliableUnordered, peer);
                                break;
                            case PacketType.RPC:
                                SendRPC(bytes, packet.Value.Target);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo info)
        {
            string disconnectedId = GetPeerId(peer);
            Packet packet = new Packet(PacketType.PeerDisconnected, "", "Disconnected", Encoding.UTF8.GetBytes(disconnectedId));
            byte[] bytes = MemoryPackSerializer.Serialize(packet);
            foreach (var player in _Players)
            {
                if(player.Key != disconnectedId)
                {
                    player.Value.Send(bytes, DeliveryMethod.ReliableOrdered);
                }
            }
        }
        /// <summary>
        /// Syncs the state to all connected peers
        /// </summary>
        /// <param name="packet">The packet</param>
        /// <param name="rawPacket">The raw packet</param>
        /// <param name="method">The </param>
        /// <param name="origin">The peer from which the state originated from</param>
        private void SyncState(Packet packet, byte[] rawPacket, DeliveryMethod method = DeliveryMethod.ReliableUnordered, NetPeer? origin = null)
        {
            if(NetManager == null) return;
            if (origin == null)
            {
                foreach (NetPeer peer in NetManager.ConnectedPeerList)
                {
                    peer.Send(rawPacket, method);
                }
            }
            else
            {
                foreach (NetPeer peer in NetManager.ConnectedPeerList)
                {
                    if (peer != origin)
                    {
                        peer.Send(rawPacket, method);
                    }
                }
            }
        }
        private void SendRPC(byte[] rawPacket, string target)
        {
            NetPeer? targetPeer = _Players.FirstOrDefault(x => x.Key == target).Value;
            if(targetPeer != null) targetPeer.Send(rawPacket, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// This function will be called when a packet is received. Return <c>null</c> to ignore the packet.
        /// </summary>
        public Func<NetPeer, Packet, Packet?>? DataReceived;

        // Utils

        /// <summary>
        /// Gets the peer id from the peer
        /// </summary>
        /// <param name="peer">The target peer</param>
        /// <returns></returns>
        public string GetPeerId(NetPeer peer)
        {
            return _Players.FirstOrDefault(x => x.Value == peer).Key ?? "";
        }
    }
}