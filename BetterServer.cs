﻿using System;
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
        /// The delay between polling events in milliseconds. Default is 15ms
        /// </summary>
        public int PollInterval { get; private set; } = 15;
        /// <summary>
        /// The max amount of players
        /// </summary>
        public int MaxPlayers { get; private set; }
        /// <summary>
        /// Whether this server allows admin users
        /// </summary>
        public bool AllowAdminUsers { get; private set; } = false;
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
        private Dictionary<string, Action<byte[]>> RegisteredRPCs { get; set; } = new Dictionary<string, Action<byte[]>>();
        private ConcurrentDictionary<string, NetPeer> _Players { get; set; } = new();
        private ConcurrentDictionary<string, bool> _Admins { get; set; } = new();
        private List<string> _Banned { get; set; } = new();
        /// <summary>
        /// Returns a read-only dictionary of the players on the server
        /// </summary>
        public ReadOnlyDictionary<string, NetPeer> Players => new ReadOnlyDictionary<string, NetPeer>(_Players);
        /// <summary>
        /// Returns a list of all the players that are admins
        /// </summary>
        public List<string> Admins => _Admins.Keys.ToList();
        /// <summary>
        /// Returns a list of all the banned IP addresses
        /// </summary>
        public List<string> Banned => _Banned;

        /// <summary>
        /// Creates a new server
        /// </summary>
        public BetterServer()
        {
            Listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }
        /// <summary>
        /// Sets the interval between polling events. Default is 15ms
        /// </summary>
        /// <param name="interval"></param>
        /// <returns>This server</returns>
        public BetterServer WithPollInterval(int interval)
        {
            PollInterval = interval;
            return this;
        }
        /// <summary>
        /// The max amount of players
        /// </summary>
        /// <param name="maxPlayers"></param>
        /// <returns>This server</returns>
        public BetterServer WithMaxPlayers(int maxPlayers)
        {
            MaxPlayers = maxPlayers;
            return this;
        }
        /// <summary>
        /// Whether this server allows admin users
        /// </summary>
        /// <param name="allowAdminUsers"></param>
        /// <returns>This server</returns>
        public BetterServer WithAdminUsers(bool allowAdminUsers)
        {
            AllowAdminUsers = allowAdminUsers;
            return this;
        }
        /// <summary>
        /// Sets the banlist for the server
        /// </summary>
        /// <param name="addresses"></param>
        /// <returns>This server</returns>
        public BetterServer WithBannedUsers(List<string> addresses)
        {
            _Banned = new List<string>(addresses);
            return this;
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
        public BetterServer Start(int port)
        {
            NetManager = new NetManager(Listener);
            try
            {
                NetManager.Start(port);
            }
            catch (Exception ex)
            {
                return this;
            }
            Thread thread = new Thread(PollEvents);
            thread.Start();
            return this;
        }
        
        /// <summary>
        /// Stops the server
        /// </summary>
        public BetterServer Stop()
        {
            NetManager?.Stop();
            NetManager = null;
            return this;
        }

        /// <summary>
        /// Kicks a player from the server
        /// </summary>
        /// <param name="id">The target player id</param>
        /// <param name="reason">The kick reason</param>
        public void Kick(string id, string reason)
        {
            if(_Players.ContainsKey(id))
            {
                NetPeer peer = _Players[id];
                Packet packet = new Packet(PacketType.Kick, "", "Kicked", Encoding.UTF8.GetBytes(reason));
                byte[] bytes = MemoryPackSerializer.Serialize(packet);
                peer.Send(bytes, DeliveryMethod.ReliableOrdered);
                peer.Disconnect(Encoding.UTF8.GetBytes("Kicked: " + reason));
            }
        }
        /// <summary>
        /// Bans a player from the server using their IP address
        /// </summary>
        /// <param name="id">The target player id</param>
        /// <param name="reason">The ban reason</param>
        public void IPBan(string id, string reason)
        {
            if(_Players.ContainsKey(id))
            {
                NetPeer peer = _Players[id];
                Packet packet = new Packet(PacketType.Ban, "", "Banned", Encoding.UTF8.GetBytes(reason));
                byte[] bytes = MemoryPackSerializer.Serialize(packet);
                _Banned.Add(peer.Address.ToString().Split(':')[0]);
                peer.Send(bytes, DeliveryMethod.ReliableOrdered);
                peer.Disconnect(Encoding.UTF8.GetBytes("Banned: " + reason));
            }
        }

        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {
            if(_Players.Count >= MaxPlayers)
            {
                string reason = "Server is full";
                request.Reject(Encoding.UTF8.GetBytes(reason));
                return;
            }
            string address = request.RemoteEndPoint.Address.ToString().Split(':')[0];
            if (_Banned.Contains(address))
            {
                string reason = "You are banned from this server";
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
                                SyncState(packet.Value, bytes, deliveryMethod, peer);
                                break;
                            case PacketType.RPC:
                                if(GetPeer(packet.Value.Target) != null)
                                {
                                    SendRPC(bytes, packet.Value.Target, RpcMode.Target, deliveryMethod);
                                }
                                else
                                {
                                    switch(packet.Value.Target)
                                    {
                                        case "self":
                                            SendRPC(bytes, GetPeerId(peer), RpcMode.Target, deliveryMethod);
                                            break;
                                        case "all":
                                            SendRPC(bytes, "", RpcMode.All, deliveryMethod);
                                            break;
                                        case "others":
                                            SendRPC(bytes, GetPeerId(peer), RpcMode.Others, deliveryMethod);
                                            break;
                                        case "server":
                                            HandleRPC(packet.Value.Key, packet.Value.Data);
                                            break;
                                        default:
                                            break;
                                    }
                                }
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
        private void SendRPC(byte[] rawPacket, string target, RpcMode mode, DeliveryMethod method)
        {
            NetPeer? targetPeer = GetPeer(target);
            switch(mode)
            {
                case RpcMode.Target:
                    if (targetPeer != null) targetPeer.Send(rawPacket, method);
                    break;
                case RpcMode.Others:
                    foreach (var player in _Players)
                    {
                        if(player.Key != target)
                        {
                            player.Value.Send(rawPacket, method);
                        }
                    }
                    break;
                case RpcMode.All:
                    foreach (var player in _Players)
                    {
                        player.Value.Send(rawPacket, method);
                    }
                    break;
                case RpcMode.Host:
                    if(targetPeer != null) targetPeer.Send(rawPacket, method);
                    break;
            }
        }
        /// <summary>
        /// Registers a Remote Procedure Call with a method name and an action to invoke
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="action">The method</param>
        /// <returns>This server</returns>
        public BetterServer RegisterRPC(string method, Action<byte[]> action)
        {
            RegisteredRPCs[method] = action;
            return this;
        }
        private void HandleRPC(string method, byte[] args)
        {
            if (RegisteredRPCs.ContainsKey(method))
            {
                RegisteredRPCs[method](args);
            }
        }

        /// <summary>
        /// This function will be called when a packet is received. Return <c>null</c> to ignore the packet.
        /// </summary>
        public Func<NetPeer, Packet, Packet?>? DataReceived;
        /// <summary>
        /// Fluent version of <c>DataReceived</c>
        /// </summary>
        /// <param name="func">Function to call when a packet is received</param>
        /// <returns>This server</returns>
        public BetterServer OnDataReceived(Func<NetPeer, Packet, Packet?> func)
        {
            DataReceived = func;
            return this;
        }

        // Utils

        /// <summary>
        /// Gets the peer id from the peer
        /// </summary>
        /// <param name="peer">The target peer</param>
        /// <returns>The id of the peer, or <c>String.Empty</c></returns>
        public string GetPeerId(NetPeer peer)
        {
            return _Players.FirstOrDefault(x => x.Value == peer).Key ?? string.Empty;
        }
        /// <summary>
        /// Attempts to get a peer by id
        /// </summary>
        /// <param name="id">The target id</param>
        /// <returns>A <c>NetPeer</c> or <c>null</c> if not found</returns>
        public NetPeer? GetPeer(string id)
        {
            return _Players.FirstOrDefault(x => x.Key == id).Value;
        }
    }
}