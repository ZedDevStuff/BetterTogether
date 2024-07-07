using BetterTogetherCore.Transports;
using BetterTogetherCore.Models;
using MemoryPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using BetterTogetherCore.Transports.LiteNetLibTransport;

namespace BetterTogetherCore
{
    /// <summary>
    /// The BetterTogether server. Create one with a max player count then use the Start method to start the server on the specified port. Set the <c>DataReceived</c> <c>Func<![CDATA[<]]>IPEndPoint, Packet, Packet<![CDATA[>]]></c> for your data validation and handling.
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
        /// The transport used by this server. Default is LiteNetLibTransport
        /// </summary>
        public IServerTransport Transport { get; private set; } = new ServerLiteNetLibTransport();
        private CancellationTokenSource? _PollToken { get; set; } = null;
        /// <summary>
        /// Global states. Those can be modified by anyone
        /// </summary>
        public StateManager GlobalStates { get; private set; } = new();
        /// <summary>
        /// Player states. Those can only be modified by the player who owns them or the server
        /// </summary>
        public ConcurrentDictionary<string, StateManager> PlayerStates { get; private set; } = new();
        private ConcurrentDictionary<string, Dictionary<string, byte[]>> _PlayerStatesToSet { get; set; } = new();
        /// <summary>
        /// The reserved states for the server. Only the server (and admins if setup correctly) can modify these states
        /// </summary>
        public List<string> ReservedStates { get; private set; } = new List<string>();
        private Dictionary<string, ServerRpcAction> RegisteredRPCs { get; set; } = new Dictionary<string, ServerRpcAction>();
        private ConcurrentDictionary<string, IPEndPoint> _Players { get; set; } = new();
        private ConcurrentDictionary<string, bool> _Admins { get; set; } = new();
        private List<string> _Banned { get; set; } = new();
        /// <summary>
        /// Returns a read-only dictionary of the players on the server
        /// </summary>
        public ReadOnlyDictionary<string, IPEndPoint> Players => new ReadOnlyDictionary<string, IPEndPoint>(_Players);
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
            Transport.ClientConnectionRequested += ClientConnectionRequested;
            Transport.ClientConnected += ClientConnected;
            Transport.DataReceived += DataReceivedFromClient;
            Transport.ClientDisconnected += ClientDisconnected;
        }
        /// <summary>
        /// Sets the interval between polling events. Default is 15ms. Only works with LiteNetLibTransport
        /// </summary>
        /// <param name="interval"></param>
        /// <returns>This server</returns>
        public BetterServer WithPollInterval(int interval)
        {
            PollInterval = interval;
            if(Transport is ServerLiteNetLibTransport liteNetLibTransport)
            {
                liteNetLibTransport.PollInterval = interval;
            }
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
        /// Sets the transport for this server
        /// </summary>
        /// <param name="transport">Instance of the transport to use</param>
        /// <returns>This server</returns>
        public BetterServer WithTransport(IServerTransport transport)
        {
            Transport.ClientConnected -= ClientConnected;
            Transport.ClientConnectionRequested -= ClientConnectionRequested;
            Transport.DataReceived -= DataReceivedFromClient;
            Transport.ClientDisconnected -= ClientDisconnected;
            Transport.Dispose();
            if (Transport is ServerLiteNetLibTransport liteNetLibTransport)
            {
                liteNetLibTransport.PollInterval = PollInterval;
            }
            Transport = transport;
            Transport.ClientConnectionRequested += ClientConnectionRequested;
            Transport.ClientConnected += ClientConnected;
            Transport.DataReceived += DataReceivedFromClient;
            Transport.ClientDisconnected += ClientDisconnected;
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
        /// <summary>
        /// Starts the server on the specified port
        /// </summary>
        /// <param name="port">The port to start the server on. Default is 9050</param>
        /// <returns><c>true</c> if the server started successfully, <c>false</c> otherwise</returns>
        public bool Start(int port = 9050)
        {
            return Transport.StartServer(port);
        }
        
        /// <summary>
        /// Stops the server and erase all states
        /// </summary>
        public BetterServer Stop()
        {
            Transport.StopServer();
            _Players.Clear();
            _Admins.Clear();
            GlobalStates.Clear();
            PlayerStates.Clear();
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
                Packet packet = new Packet(PacketType.Kick, "", "Kicked", Encoding.UTF8.GetBytes(reason));
                Transport.SendTo(_Players[id], packet.Pack(), DeliveryMethod.ReliableOrdered);
                Transport.DisconnectClient(_Players[id] ,"Kicked: " + reason);
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
                Packet packet = new Packet(PacketType.Ban, "", "Banned", Encoding.UTF8.GetBytes(reason));
                _Banned.Add(_Players[id].Address.ToString());
                Transport.SendTo(_Players[id], packet.Pack(), DeliveryMethod.ReliableOrdered);
                Transport.DisconnectClient(_Players[id], "Banned: " + reason);
            }
        }

        private void ClientConnectionRequested(ConnectionRequest request)
        {
            if (_Players.Count == MaxPlayers)
            {
                string reason = "Server is full";
                request.Reject(reason);
                return;
            }
            if (_Banned.Contains(request.EndPoint.Address.ToString()))
            {
                string reason = "You are banned from this server";
                request.Reject(reason);
                return;
            }
            if (request.ConnectionData.Key == "BetterTogether")
            {
                request.Accept();
                if(request.ConnectionData.ExtraData.ContainsKey("states"))
                {
                    StateManager ? states = new StateManager(MemoryPackSerializer.Deserialize<Dictionary<string, byte[]>>(request.ConnectionData.ExtraData["states"])!);
                    if (states == null) return;
                    foreach (var state in states.States)
                    {
                        if (ReservedStates.Contains(state.Key)) continue;
                        if (state.Key.FastStartsWith("[player]"))
                        {
                            string ipPort = request.EndPoint.ToString();
                            if (!_PlayerStatesToSet.ContainsKey(ipPort)) _PlayerStatesToSet[ipPort] = new Dictionary<string, byte[]>();
                            if (_PlayerStatesToSet[ipPort].ContainsKey(state.Key)) _PlayerStatesToSet[ipPort][state.Key] = state.Value;
                            _PlayerStatesToSet[ipPort][state.Key.Replace("[player]", "")] = state.Value;
                        }
                        else GlobalStates[state.Key] = state.Value;
                    }
                }
            }
            else
            {
                string reason = "Invalid key";
                request.Reject(reason);
            }
        }
        private void ClientConnected(IPEndPoint client)
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
            string ipPort = client.ToString();
            if(_PlayerStatesToSet.ContainsKey(ipPort))
            {
                foreach (var state in _PlayerStatesToSet[ipPort])
                {
                    if(ReservedStates.Contains(state.Key)) continue;
                    if(PlayerStates.ContainsKey(id)) PlayerStates[id].SetState(state.Key, state.Value);
                    else
                    {
                        PlayerStates[id] = new StateManager();
                        PlayerStates[id].SetState(state.Key, state.Value);
                    }
                }
                _PlayerStatesToSet.TryRemove(ipPort, out _);
            }
            List<string> players = [id,.._Players.Keys];
            Packet packet1 = new Packet(PacketType.PeerConnected, "", "Connected", Encoding.UTF8.GetBytes(id));
            Transport.Broadcast(packet1.Pack(), DeliveryMethod.ReliableOrdered);
            _Players[id] = client;
            byte[] data = MemoryPackSerializer.Serialize(players);
            Packet packet2 = new Packet(PacketType.SelfConnected, "", "Connected", data);
            Transport.SendTo(client, packet2.Pack(), DeliveryMethod.ReliableOrdered);
            Dictionary<string, byte[]> initData = new Dictionary<string, byte[]>();
            initData["globalStates"] = MemoryPackSerializer.Serialize(GlobalStates);
            initData["playerStates"] = MemoryPackSerializer.Serialize(PlayerStates);
            Packet packet3 = Packet.New(PacketType.Init, "", "Init", initData);
            Transport.SendTo(client, packet3.Pack(), DeliveryMethod.ReliableOrdered);
        }
        private void DataReceivedFromClient(IPEndPoint client, byte[] data, DeliveryMethod method)
        {
            Packet? packet = MemoryPackSerializer.Deserialize<Packet>(data);
            if (packet != null)
            {
                string origin = GetClientId(client);
                if (DataReceived != null) packet = DataReceived(client, packet);
                if (packet != null)
                {
                    switch (packet.Type)
                    {
                        case PacketType.Ping:
                            if(packet.Target == "server") Transport.SendTo(client, data, method);
                            else
                            {
                                IPEndPoint? targetPeer = _Players.FirstOrDefault(x => x.Key == packet.Target).Value;
                                if (origin != String.Empty && targetPeer != null)
                                {
                                    if(packet.Key == "pong")
                                    {
                                        Packet pong = new Packet();
                                        pong.Type = PacketType.Ping;
                                        pong.Target = packet.Target;
                                        Transport.SendTo(targetPeer, pong.Pack(), method);
                                    }
                                    else
                                    {
                                        Packet ping = new Packet();
                                        ping.Type = PacketType.Ping;
                                        ping.Target = origin;
                                        Transport.SendTo(targetPeer, ping.Pack(), method);
                                    }
                                }
                            }
                            break;
                        case PacketType.SetState:
                            if (packet.Target.Length == 36)
                            {
                                if (packet.Target == origin)
                                {
                                    if(PlayerStates.ContainsKey(origin)) PlayerStates[origin].SetState(packet.Key, packet.Data);
                                    else
                                    {
                                        PlayerStates[origin] = new StateManager();
                                        PlayerStates[origin].SetState(packet.Key, packet.Data);
                                    }
                                    SyncState(packet, data, method, client);
                                }
                            }
                            else
                            {
                                if(ReservedStates.Contains(packet.Key))
                                {
                                    byte[] emptyData = [];
                                    if(GlobalStates.ContainsKey(packet.Key)) data = GlobalStates[packet.Key];
                                    else GlobalStates[packet.Key] = data;
                                    Packet response = new Packet(PacketType.SetState, "FORBIDDEN", packet.Key, data);
                                    Transport.SendTo(client, response.Pack(), DeliveryMethod.ReliableOrdered);
                                }
                                GlobalStates[packet.Key] = packet.Data;
                                SyncState(packet, data, method, client);
                            }
                            break;
                        case PacketType.RPC:
                            if(GetClient(packet.Target) != null)
                            {
                                SendRPC(data, packet.Target, RpcMode.Target, method);
                            }
                            else
                            {
                                switch(packet.Target)
                                {
                                    case "self":
                                        SendRPC(data, origin, RpcMode.Target, method);
                                        break;
                                    case "all":
                                        Packet allPacket = new Packet(packet.Type, origin, packet.Key, packet.Data);
                                        SendRPC(allPacket.Pack(), "", RpcMode.All, method);
                                        break;
                                    case "others":
                                        Packet othersPacket = new Packet(packet.Type, origin, packet.Key, packet.Data);
                                        SendRPC(othersPacket.Pack(), origin, RpcMode.Others, method);
                                        break;
                                    case "server":
                                        HandleRPC(packet.Key, packet.Data, client);
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
        private void ClientDisconnected(IPEndPoint peer, DisconnectInfo info)
        {
            string disconnectedId = GetClientId(peer);
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
        private void SyncState(Packet packet, byte[] rawPacket, DeliveryMethod method = DeliveryMethod.ReliableUnordered, IPEndPoint? origin = null)
        {
            if (origin == null)
            {
                Transport.Broadcast(rawPacket, method);
            }
            else
            {
                SendAll(rawPacket, method, origin);
            }
        }
        /// <summary>
        /// Deletes a global state on the server and all connected clients
        /// </summary>
        /// <param name="key"></param>
        public void DeleteGlobalState(string key)
        {
            GlobalStates.Remove(key);
            Packet packet = new Packet(PacketType.DeleteState, "", "GlobalDelete", Encoding.UTF8.GetBytes(key));
            Transport.Broadcast(packet.Pack(), DeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Clears all global states on the server and all connected clients except the specified states
        /// </summary>
        /// <param name="except">The states to exclude</param>
        public void ClearGlobalStatesExcept(IEnumerable<string> except)
        {
            GlobalStates.ClearExcept(except);
            Packet packet = Packet.New(PacketType.DeleteState, "", "GlobalClearExcept", except);
            Transport.Broadcast(packet.Pack(), DeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Clears all global states on the server and all connected clients including the specified states
        /// </summary>
        /// <param name="including"></param>
        public void ClearGlobalStatesIncluding(IEnumerable<string> including)
        {
            GlobalStates.ClearIncluding(including);
            Packet packet = Packet.New(PacketType.DeleteState, "", "GlobalClearIncluding", including);
            Transport.Broadcast(packet.Pack(), DeliveryMethod.ReliableOrdered);
        }
        private void SendRPC(byte[] rawPacket, string target, RpcMode mode, DeliveryMethod method)
        {
            IPEndPoint? targetPeer = GetClient(target);
            switch(mode)
            {
                case RpcMode.Target:
                    if (targetPeer != null) Transport.SendTo(targetPeer, rawPacket, method);
                    break;
                case RpcMode.Others:
                    SendAll(rawPacket, method, targetPeer);
                    break;
                case RpcMode.All:
                    SendAll(rawPacket, method);
                    break;
                case RpcMode.Host:
                    if(targetPeer != null) Transport.SendTo(targetPeer, rawPacket, method);
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
        public delegate void ServerRpcAction(IPEndPoint? peer, byte[] args);
        private void HandleRPC(string method, byte[] args, IPEndPoint? peer = null)
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
        public Func<IPEndPoint, Packet, Packet?>? DataReceived;
        /// <summary>
        /// Fluent version of <c>DataReceived</c>
        /// </summary>
        /// <param name="func">Function to call when a packet is received</param>
        /// <returns>This server</returns>
        public BetterServer OnDataReceived(Func<IPEndPoint, Packet, Packet?> func)
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
        public void SendAll(byte[] data, DeliveryMethod method, IPEndPoint? except = null)
        {
            foreach (var player in _Players)
            {
                if(player.Value != except)
                {
                    Transport.SendTo(player.Value, data, method);
                }
            }
        }
        /// <summary>
        /// Gets the peer id from the peer
        /// </summary>
        /// <param name="peer">The target peer</param>
        /// <returns>The id of the peer, or <c>String.Empty</c></returns>
        public string GetClientId(IPEndPoint peer)
        {
            return _Players.FirstOrDefault(x => x.Value == peer).Key ?? string.Empty;
        }
        /// <summary>
        /// Attempts to get a peer by id
        /// </summary>
        /// <param name="id">The target id</param>
        /// <returns>A <c>IPEndPoint</c> or <c>null</c> if not found</returns>
        public IPEndPoint? GetClient(string id)
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