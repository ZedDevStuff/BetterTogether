using BetterTogetherCore.State;
using LiteNetLib;
using MemoryPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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
        public int MaxPlayers { get; private set; } = 10;
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
        private CancellationTokenSource? _PollToken { get; set; } = null;
        /// <summary>
        /// Global states. Those can be modified by anyone
        /// </summary>
        public StateManager GlobalStates { get; private set; } = new();
        /// <summary>
        /// Player states. Those can only be modified by the player who owns them
        /// </summary>
        public StateManager PlayerStates { get; private set; } = new();
        private ConcurrentDictionary<string, Dictionary<string, byte[]>> _PlayerStatesToSet { get; set; } = new();
        /// <summary>
        /// The reserved states for the server. Only the server (and admins if setup correctly) can modify these states
        /// </summary>
        public List<string> ReservedStates { get; private set; } = new List<string>();
        private Dictionary<string, ServerRpcAction> RegisteredRPCs { get; set; } = new Dictionary<string, ServerRpcAction>();
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
        /// <summary>
        /// Sets the reserved states for the server
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public BetterServer WithReservedStates(List<string> states)
        {
            ReservedStates = new List<string>(states);
            return this;
        }
        private void PollEvents()
        {
            while (true)
            {
                if (_PollToken?.IsCancellationRequested == true) break;
                NetManager?.PollEvents();
                Thread.Sleep(15);
            }
        }
        /// <summary>
        /// Starts the server on the specified port
        /// </summary>
        /// <param name="port">The port to start the server on. Default is 9050</param>
        /// <returns><c>true</c> if the server started successfully, <c>false</c> otherwise</returns>
        public bool Start(int port = 9050)
        {
            NetManager = new NetManager(Listener);
            try
            {
                if(NetManager.Start(port))
                {
                    _PollToken = new CancellationTokenSource();
                    Thread thread = new Thread(PollEvents);
                    thread.Start();
                    return true;
                }
                else
                {
                    NetManager = null;
                    return false;
                }
            }
            catch
            {
                NetManager?.Stop();
                NetManager = null;
                return false;
            }
        }
        
        /// <summary>
        /// Stops the server
        /// </summary>
        public BetterServer Stop()
        {
            _PollToken?.Cancel();
            if(NetManager == null) return this;
            _Players.Clear();
            _Admins.Clear();
            GlobalStates.Clear();
            PlayerStates.Clear();
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
                peer.Send(packet.Pack(), DeliveryMethod.ReliableOrdered);
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
                _Banned.Add(peer.Address.ToString().Split(':')[0]);
                peer.Send(packet.Pack(), DeliveryMethod.ReliableOrdered);
                peer.Disconnect(Encoding.UTF8.GetBytes("Banned: " + reason));
            }
        }

        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {
            if(request.Data.UserDataSize == 0) return;
            byte[] bytes = request.Data.RawData[request.Data.UserDataOffset..(request.Data.UserDataOffset + request.Data.UserDataSize)];
            Transport.ConnectionData? data = MemoryPackSerializer.Deserialize<Transport.ConnectionData>(bytes);
            if(data == null) return;
            if (_Players.Count == MaxPlayers)
            {
                string reason = "Server is full";
                request.Reject(Encoding.UTF8.GetBytes(reason));
                return;
            }
            string ip = request.RemoteEndPoint.Address.ToString();
            if (_Banned.Contains(ip))
            {
                string reason = "You are banned from this server";
                request.Reject(Encoding.UTF8.GetBytes(reason));
                return;
            }
            if (data.Key == "BetterTogether")
            {
                request.Accept();
                StateManager? states = MemoryPackSerializer.Deserialize<StateManager>(data.ExtraData["states"]);
                if(states == null) return;
                foreach (var state in states.States)
                {
                    if (ReservedStates.Contains(state.Key)) continue;
                    if (state.Key.FastStartsWith("[player]"))
                    {
                        string ipPort = request.RemoteEndPoint.ToString();
                        if(!_PlayerStatesToSet.ContainsKey(ipPort)) _PlayerStatesToSet[ipPort] = new Dictionary<string, byte[]>();
                        if (_PlayerStatesToSet[ipPort].ContainsKey(state.Key)) _PlayerStatesToSet[ipPort][state.Key] = state.Value;
                        _PlayerStatesToSet[ipPort][state.Key.Replace("[player]", "")] = state.Value;
                    }
                    else GlobalStates[state.Key] = state.Value;
                }
            }
            else
            {
                string reason = "Invalid key";
                request.Reject(Encoding.UTF8.GetBytes(reason));
            }
        }
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            string id = Guid.NewGuid().ToString();
            while(_Players.ContainsKey(id))
            {
                id = Guid.NewGuid().ToString();
            }
            if(AllowAdminUsers && _Players.Count == 0)
            {
                _Admins[id] = true;
            }
            string ipPort = peer.Address.ToString() + ":" + peer.Port;
            if(_PlayerStatesToSet.ContainsKey(ipPort))
            {
                foreach (var state in _PlayerStatesToSet[ipPort])
                {
                    PlayerStates[id+state.Key] = state.Value;
                }
                _PlayerStatesToSet.TryRemove(ipPort, out _);
            }
            List<string> players = [id,.._Players.Keys];
            Packet packet1 = new Packet(PacketType.PeerConnected, "", "Connected", Encoding.UTF8.GetBytes(id));
            SendAll(packet1.Pack(), DeliveryMethod.ReliableOrdered, peer);
            _Players[id] = peer;
            byte[] data = MemoryPackSerializer.Serialize(players);
            Packet packet2 = new Packet(PacketType.SelfConnected, "", "Connected", data);
            peer.Send(packet2.Pack(), DeliveryMethod.ReliableOrdered);
            byte[] states = MemoryPackSerializer.Serialize(GlobalStates.States);
            Packet packet3 = new Packet(PacketType.Init, "", "Init", states);
            peer.Send(packet3.Pack(), DeliveryMethod.ReliableOrdered);
        }
        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes > 0)
            {
                string origin = GetPeerId(peer);
                byte[] bytes = reader.GetRemainingBytes();
                Packet? packet = MemoryPackSerializer.Deserialize<Packet>(bytes);
                if (packet != null)
                {
                    if (DataReceived != null) packet = DataReceived(peer, packet);
                    if (packet != null)
                    {
                        switch (packet.Type)
                        {
                            case PacketType.Ping:
                                if(packet.Target == "server") peer.Send(bytes, deliveryMethod);
                                else
                                {
                                    NetPeer? targetPeer = _Players.FirstOrDefault(x => x.Key == packet.Target).Value;
                                    if (origin != null && targetPeer != null)
                                    {
                                        if(packet.Key == "pong")
                                        {
                                            Packet pong = new Packet();
                                            pong.Type = PacketType.Ping;
                                            pong.Target = packet.Target;
                                            targetPeer.Send(pong.Pack(), deliveryMethod);
                                        }
                                        else
                                        {
                                            Packet ping = new Packet();
                                            ping.Type = PacketType.Ping;
                                            ping.Target = origin;
                                            targetPeer.Send(ping.Pack(), deliveryMethod);
                                        }
                                    }
                                }
                                break;
                            case PacketType.SetState:
                                if (packet.Target.Length == 36)
                                {
                                    if (packet.Target == origin)
                                    {
                                        PlayerStates[origin + packet.Key] = packet.Data;
                                        SyncState(packet, bytes, deliveryMethod, peer);
                                    }
                                }
                                else
                                {
                                    if(ReservedStates.Contains(packet.Key))
                                    {
                                        byte[] data = [];
                                        if(GlobalStates.ContainsKey(packet.Key)) data = GlobalStates[packet.Key];
                                        else GlobalStates[packet.Key] = data;
                                        Packet response = new Packet(PacketType.SetState, "FORBIDDEN", packet.Key, data);
                                        peer.Send(response.Pack(), DeliveryMethod.ReliableOrdered);
                                    }
                                    GlobalStates[packet.Key] = packet.Data;
                                    SyncState(packet, bytes, deliveryMethod, peer);
                                }
                                break;
                            case PacketType.RPC:
                                if(GetPeer(packet.Target) != null)
                                {
                                    SendRPC(bytes, packet.Target, RpcMode.Target, deliveryMethod);
                                }
                                else
                                {
                                    string peerId = GetPeerId(peer);
                                    switch(packet.Target)
                                    {
                                        case "self":
                                            SendRPC(bytes, peerId, RpcMode.Target, deliveryMethod);
                                            break;
                                        case "all":
                                            Packet allPacket = new Packet(packet.Type, peerId, packet.Key, packet.Data);
                                            SendRPC(allPacket.Pack(), "", RpcMode.All, deliveryMethod);
                                            break;
                                        case "others":
                                            Packet othersPacket = new Packet(packet.Type, peerId, packet.Key, packet.Data);
                                            SendRPC(othersPacket.Pack(), peerId, RpcMode.Others, deliveryMethod);
                                            break;
                                        case "server":
                                            HandleRPC(packet.Key, packet.Data, peer);
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
            _Admins.TryRemove(disconnectedId, out _);
            Packet packet = new Packet(PacketType.PeerDisconnected, "", "Disconnected", Encoding.UTF8.GetBytes(disconnectedId));
            SendAll(packet.Pack(), DeliveryMethod.ReliableOrdered, peer);
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
        /// <summary>
        /// Deletes the state with the specified key
        /// </summary>
        /// <param name="key">The key to delete</param>
        public void DeleteState(string key)
        {
            GlobalStates.Remove(key);
            Packet delete = new Packet(PacketType.DeleteState, "global", key, [0]);
            SendAll(delete.Pack(), DeliveryMethod.ReliableUnordered);
        }
        /// <summary>
        /// Clears all global states except for the specified keys
        /// </summary>
        /// <param name="except">Keys to keep</param>
        public void ClearAllGlobalStates(List<string> except)
        {
            
            var globalStates = GlobalStates.States.Where(x => !Utils.guidRegex.IsMatch(x.Key) && !except.Contains(x.Key)).ToList();
            foreach (var state in globalStates)
            {
                GlobalStates.Remove(state.Key);
            }
            Packet delete = new Packet(PacketType.DeleteState, "global", "", MemoryPackSerializer.Serialize(except));
            SendAll(delete.Pack(), DeliveryMethod.ReliableUnordered);

        }
        /// <summary>
        /// Deletes the player state with the specified key
        /// </summary>
        /// <param name="player">The player id</param>
        /// <param name="key">The key to delete</param>
        public void DeletePlayerState(string player, string key)
        { 
            PlayerStates.Remove(player + key);
            Packet delete = new Packet(PacketType.DeleteState, player, key, [0]);
            SendAll(delete.Pack(), DeliveryMethod.ReliableUnordered);
        }
        /// <summary>
        /// Clears all player states for the specific player except for the specified keys
        /// </summary>
        /// <param name="player"></param>
        /// <param name="except">The keys to keep</param>
        public void ClearSpecificPlayerStates(string player, List<string> except)
        {
            PlayerStates.ClearExcept(except);
            Packet delete = new Packet(PacketType.DeleteState, player, "", MemoryPackSerializer.Serialize(except));
            SendAll(delete.Pack(), DeliveryMethod.ReliableUnordered);
        }
        /// <summary>
        /// Clears all player states except for the specified keys
        /// </summary>
        /// <param name="except">The keys to keep</param>
        public void ClearAllPlayerStates(List<string> except)
        {
            var playerStates = GlobalStates.States.Where(x => StartsWithGuid(x.Key) && !except.Contains(x.Key.Substring(0, 36))).ToList();
            foreach (var state in playerStates)
            {
                GlobalStates.Remove(state.Key);
            }
            Packet delete = new Packet(PacketType.DeleteState, "players", "", MemoryPackSerializer.Serialize(except));
            SendAll(delete.Pack(), DeliveryMethod.ReliableUnordered);
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
                    SendAll(rawPacket, method, targetPeer);
                    break;
                case RpcMode.All:
                    SendAll(rawPacket, method);
                    break;
                case RpcMode.Host:
                    if(targetPeer != null) targetPeer.Send(rawPacket, method);
                    break;
            }
        }
        /// <summary>
        /// Registers a Remote Procedure Call with a method name and an action to invoke.
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="action">The method</param>
        /// <returns>This server</returns>
        public BetterServer RegisterRPC(string method, ServerRpcAction action)
        {
            RegisteredRPCs[method] = action;
            return this;
        }
        /// <summary>
        /// A delegate for RPC actions on the server
        /// </summary>
        /// <param name="peer">The peer that invoked the RPC</param>
        /// <param name="args">The MemoryPacked arguments</param>
        public delegate void ServerRpcAction(NetPeer? peer, byte[] args);
        private void HandleRPC(string method, byte[] args, NetPeer? peer = null)
        {
            if (RegisteredRPCs.ContainsKey(method))
            {
                RegisteredRPCs[method](peer, args);
            }
        }
        /// <summary>
        /// Calls a registered RPC on this server
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The arguments. Must be MemoryPackable</param>
        public void RpcSelf(string method, byte[] args)
        {
            HandleRPC(method, args);
        }
        /// <summary>
        /// Calls a registered RPC on this server
        /// </summary>
        /// <typeparam name="T">The type of the arguments. Must be MemoryPackable</typeparam>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The arguments. Must be MemoryPackable</param>
        public void RpcSelf<T>(string method, T args)
        {
            byte[] bytes = MemoryPackSerializer.Serialize(args);
            HandleRPC(method, bytes);
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
        /// Sends a packet to everyone except the specified peer
        /// </summary>
        /// <param name="data">The packet data</param>
        /// <param name="method">The delivery method</param>
        /// <param name="except">The peer to exclude</param>
        public void SendAll(byte[] data, DeliveryMethod method, NetPeer? except = null)
        {
            foreach (var player in _Players)
            {
                if(player.Value != except)
                {
                    player.Value.Send(data, method);
                }
            }
        }
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
        /// <summary>
        /// Checks if a string is a valid GUID
        /// </summary>
        /// <param name="id"></param>
        /// <returns><c>true</c> if the string is a valid GUID, <c>false</c> otherwise</returns>
        public static bool IsGuid(string id)
        {
            if (id.Length != 36) return false;
            return Utils.guidRegex.IsMatch(id);
        }
        /// <summary>
        /// Checks if a string starts with a GUID
        /// </summary>
        /// <param name="id"></param>
        /// <returns><c>true</c> if the string starts with a GUID, <c>false</c> otherwise</returns>
        public static bool StartsWithGuid(string id)
        {
            if (id.Length < 36) return false;
            return Utils.guidRegex.IsMatch(id.Substring(0, 36));
        }
        /// <summary>
        /// Checks if a player is an admin
        /// </summary>
        /// <param name="id">The target id</param>
        /// <returns><c>true</c> if the player is an admin, <c>false</c> otherwise</returns>
        public bool IsAdmin(string id)
        {
            return _Admins.ContainsKey(id);
        }
    }
}