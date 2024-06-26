﻿using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace BetterTogetherCore
{
    /// <summary>
    /// A BetterTogether client that connects to a BetterTogether server
    /// </summary>
    public class BetterClient
    {
        /// <summary>
        /// The id assigned to this client by the server
        /// </summary>
        public string Id { get; private set; } = "";
        /// <summary>
        /// The delay between polling events in milliseconds. Default is 15ms
        /// </summary>
        public int PollInterval { get; private set; } = 15;
        private List<string> _Players { get; set; } = new List<string>();
        /// <summary>
        /// Returns a list of all connected players
        /// </summary>
        public List<string> Players => new List<string>(_Players);
        /// <summary>
        /// The underlying <c>LiteNetLib.NetManager</c>
        /// </summary>
        public NetManager? NetManager { get; private set; } = null;
        /// <summary>
        /// The underlying <c>LiteNetLib.EventBasedNetListener</c>
        /// </summary>
        public EventBasedNetListener Listener { get; private set; } = new EventBasedNetListener();
        private CancellationTokenSource? _PollToken { get; set; } = null;
        private Dictionary<string, byte[]> _InitStates { get; set; } = new Dictionary<string, byte[]>();
        private ConcurrentDictionary<string, byte[]> _States { get; set; } = new();
        private Dictionary<string, ClientRpcAction> RegisteredRPCs { get; set; } = new Dictionary<string, ClientRpcAction>();
        private Dictionary<string, Action<Packet>> RegisteredEvents { get; set; } = new Dictionary<string, Action<Packet>>();

        /// <summary>
        /// Creates a new BetterClient
        /// </summary>
        public BetterClient()
        {
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }
        /// <summary>
        /// Sets the interval between polling events. Default is 15ms
        /// </summary>
        /// <param name="interval"></param>
        /// <returns>This client</returns>
        public BetterClient WithPollInterval(int interval)
        {
            PollInterval = interval;
            return this;
        }
        /// <summary>
        /// Sets the initial states of the client
        /// </summary>
        /// <param name="states"></param>
        /// <returns>This client</returns>
        public BetterClient WithInitStates(Dictionary<string, byte[]> states)
        {
            _InitStates = states;
            return this;
        }
        private void PollEvents()
        {
            while (NetManager != null)
            {
                if (_PollToken?.IsCancellationRequested == true) return;
                NetManager.PollEvents();
                Thread.Sleep(PollInterval);
            }
        }
        /// <summary>
        /// Connects the client to the target server
        /// </summary>
        /// <param name="host">The address of the server</param>
        /// <param name="port">The port of the server</param>
        /// <returns>True if the connection was successful</returns>
        public bool Connect(string host, int port = 9050)
        {
            NetManager = new NetManager(Listener);
            try
            {
                NetManager.Start();
                ConnectionData connectionData = new ConnectionData("BetterTogether", _InitStates);
                NetDataWriter writer = new NetDataWriter();
                byte[] data = MemoryPackSerializer.Serialize(connectionData);
                Console.WriteLine(data.Length);
                writer.Put(data);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(host), port);
                if(NetManager.Connect(endPoint, writer) != null)
                {
                    _PollToken = new CancellationTokenSource();
                    Thread thread = new Thread(PollEvents);
                    thread.Start();
                    return true;
                }
                else return false;
            }
            catch
            {
                NetManager.Stop();
                NetManager = null;
                return false;
            }
        }
        /// <summary>
        /// Disconnects the client from the server
        /// </summary>
        /// <returns>This client</returns>
        public BetterClient Disconnect()
        {
            _PollToken?.Cancel();
            if (NetManager == null) return this;
            Id = "";
            _Players.Clear();
            _States.Clear();
            NetManager.DisconnectPeer(NetManager.FirstPeer);
            NetManager?.Stop();
            NetManager = null;
            return this;
        }
        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes > 0)
            {
                byte[] bytes = reader.GetRemainingBytes();
                Packet packet = MemoryPackSerializer.Deserialize<Packet>(bytes);
                switch (packet.Type)
                {
                    case PacketType.Ping:
                        if(packet.Target == "server") _Ping = DateTime.Now;
                        else if(packet.Target == Id)
                        {
                            _Ping = DateTime.Now;
                        }
                        else if(packet.Target != "server")
                        {
                            Packet pong = new Packet();
                            pong.Type = PacketType.Ping;
                            pong.Target = packet.Target;
                            pong.Key = "pong";
                            peer.Send(pong.Pack(), deliveryMethod);
                        }
                        break;
                    case PacketType.SetState:
                        if(packet.Target == "FORBIDEN")
                        {
                            _States[packet.Key] = packet.Data;
                        }
                        _States[packet.Key] = packet.Data;
                        if (RegisteredEvents.ContainsKey(packet.Key))
                        {
                            RegisteredEvents[packet.Key](packet);
                        }
                        break;
                    case PacketType.Init:
                        var states = packet.GetData<ConcurrentDictionary<string, byte[]>>();
                        if(states != null) _States = states;
                        break;
                    case PacketType.RPC:
                        HandleRPC(packet.Key, packet.Target, packet.Data);
                        break;
                    case PacketType.DeleteState:
                        List<string>? except = packet.GetData<List<string>>();
                        if(packet.Target.Length == 36 && Players.Contains(packet.Target))
                        {
                            if(packet.Key != "")
                            {
                                DeletePlayerState(packet.Target, packet.Key);
                            }
                            else if(except != null)
                            {
                                ClearSpecificPlayerStates(packet.Target, except);
                            }
                        }
                        else if(packet.Target == "players")
                        {
                            if(except != null)
                            {
                                ClearAllPlayerStates(except);
                            }
                        }
                        else if(packet.Target == "global")
                        {
                            if(packet.Key != "")
                            {
                                DeleteState(packet.Key);
                            }
                            if(except != null)
                            {
                                ClearAllGlobalStates(except);
                            }
                        }
                        break;
                    case PacketType.SelfConnected:
                        List<string>? list = packet.GetData<List<string>>();
                        if (list != null && list.Count > 0)
                        {
                            Id = list[0];
                            _Players = list;
                            list.Remove(Id);
                            Connected?.Invoke(Id, list);
                        }
                        break;
                    case PacketType.PeerConnected:
                        string connectedId = Encoding.UTF8.GetString(packet.Data);
                        _Players.Add(connectedId);
                        PlayerConnected?.Invoke(connectedId);
                        break;
                    case PacketType.PeerDisconnected:
                        string disconnectedId = Encoding.UTF8.GetString(packet.Data);
                        _Players.Remove(disconnectedId);
                        PlayerDisconnected?.Invoke(disconnectedId);
                        break;
                    case PacketType.Kick:
                        Kicked?.Invoke(Encoding.UTF8.GetString(packet.Data));
                        break;
                    case PacketType.Ban:
                        Banned?.Invoke(Encoding.UTF8.GetString(packet.Data));
                        break;
                    default:
                        break;
                }
            }
        }

        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Disconnected?.Invoke(disconnectInfo);
        }
        
        /// <summary>
        /// Sends a state object to the server
        /// </summary>
        /// <param name="key">The name of the state to set</param>
        /// <param name="data">The MemoryPacked object</param>
        /// <param name="method">The delivery method of LiteNetLib</param>
        public void SetState(string key, byte[] data, DeliveryMethod method = DeliveryMethod.ReliableUnordered)
        {
            if(NetManager == null) return;
            if(key.Length >= 36 && Utils.guidRegex.IsMatch(key)) return;
            _States[key] = data;
            Packet packet = new Packet
            {
                Type = PacketType.SetState,
                Key = key,
                Data = data
            };
            NetManager.FirstPeer.Send(packet.Pack(), method);
        }
        
        /// <summary>
        /// Sends a state object to the server. This state object is owned by the player and only this client or the server can modify it
        /// </summary>
        /// <param name="key">The name of the state to set</param>
        /// <param name="data">The MemoryPacked object</param>
        /// <param name="method">The delivery method of LiteNetLib</param>
        public void SetPlayerState(string key, byte[] data, DeliveryMethod method = DeliveryMethod.ReliableUnordered)
        {
            if (NetManager == null) return;
            _States[Id + key] = data;
            Packet packet = new Packet
            {
                Type = PacketType.SetState,
                Target = Id,
                Key = Id + key,
                Data = data
            };
            NetManager.FirstPeer.Send(packet.Pack(), method);
        }
        
        /// <summary>
        /// Sends a state object to the server
        /// </summary>
        /// <typeparam name="T">The type of the object. Must be MemoryPackable</typeparam>
        /// <param name="key">The name of the state to set</param>
        /// <param name="data">The object to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method of LiteNetLib</param>
        public void SetState<T>(string key, T data, DeliveryMethod method = DeliveryMethod.ReliableUnordered)
        {
            SetState(key, MemoryPackSerializer.Serialize(data), method);
        }
        
        /// <summary>
        /// Sends a state object to the server. This state object is owned by the player and only this client or the server can modify it
        /// </summary>
        /// <typeparam name="T">The type of the object. Must be MemoryPackable</typeparam>
        /// <param name="key">The name of the state to set</param>
        /// <param name="data">The object to send. Must be MemoryPackable</param>
        /// <param name="method"></param>
        public void SetPlayerState<T>(string key, T data, DeliveryMethod method = DeliveryMethod.ReliableUnordered)
        {
            SetPlayerState(key, MemoryPackSerializer.Serialize(data), method);
        }
        
        /// <summary>
        /// Gets the latest state of a key available on this client
        /// </summary>
        /// <typeparam name="T">The expected type of the object. Must be MemoryPackable</typeparam>
        /// <param name="key">The name of the state object</param>
        /// <returns>The deserialized object or the default value of the expected type</returns>
        public T? GetState<T>(string key)
        {
            if (_States.ContainsKey(key))
            {
                return MemoryPackSerializer.Deserialize<T>(_States[key]);
            }
            return default(T);
        }

        /// <summary>
        /// Gets the latest state of a player specific key available on this client
        /// </summary>
        /// <typeparam name="T">The expected type of the object. Must be MemoryPackable</typeparam>
        /// <param name="playerId">The id of the player</param>
        /// <param name="key">The name of the state object</param>
        /// <returns>The deserialized object or the default value of the expected type</returns>
        public T? GetPlayerState<T>(string playerId, string key)
        {
            string finalKey = playerId + key;
            if (_States.ContainsKey(finalKey))
            {
                return MemoryPackSerializer.Deserialize<T>(_States[finalKey]);
            }
            return default(T);
        }

        private void DeleteState(string key)
        {
            _States.TryRemove(key, out _);
        }
        private void ClearAllGlobalStates(List<string> except)
        {
            var globalStates = _States.Where(x => !Utils.guidRegex.IsMatch(x.Key) && !except.Contains(x.Key)).ToList();
            foreach (var state in globalStates)
            {
                _States.TryRemove(state.Key, out _);
            }
        }
        private void DeletePlayerState(string player, string key)
        {
            _States.TryRemove(player + key, out _);
        }
        private void ClearSpecificPlayerStates(string player, List<string> except)
        {
            foreach (var key in except)
            {
                _States.TryRemove(player + key, out _);
            }
        }
        private void ClearAllPlayerStates(List<string> except)
        {
            Regex regex = new Regex(@"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}");
            var playerStates = _States.Where(x => regex.IsMatch(x.Key) && !except.Contains(x.Key.Substring(0, 36))).ToList();
            foreach (var state in playerStates)
            {
                _States.TryRemove(state.Key, out _);
            }
        }

        /// <summary>
        /// Registers a Remote Procedure Call with a method name and an action to invoke
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="action">The method</param>
        /// <returns>This client</returns>
        public BetterClient RegisterRPC(string method, ClientRpcAction action)
        {
            RegisteredRPCs[method] = action;
            return this;
        }
        private void HandleRPC(string method, string player, byte[] args)
        {
            if(RegisteredRPCs.ContainsKey(method))
            {
                RegisteredRPCs[method](player, args);
            }
        }
        
        /// <summary>
        /// Sends a Remote Procedure Call to the server then back to this client
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The MemoryPacked arguments for the method</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcSelf(string method, byte[] args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            if (NetManager == null) return this;
            Packet packet = new Packet
            {
                Type = PacketType.RPC,
                Target = "self",
                Key = method,
                Data = args
            };
            NetManager.FirstPeer.Send(packet.Pack(), delMethod);
            return this;
        }
        /// <summary>
        /// Sends a Remote Procedure Call to the server then back to this client
        /// </summary>
        /// <typeparam name="T">The type of the arguments. Must be MemoryPackable</typeparam>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The arguments for the method. Must be MemoryPackable</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcSelf<T>(string method, T args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            byte[] bytes = MemoryPackSerializer.Serialize(args);
            return RpcSelf(method, bytes, delMethod);
        }

        /// <summary>
        /// Sends a Remote Procedure Call to the target player
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="target">The id of the target player</param>
        /// <param name="args">The MemoryPacked arguments for the method</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcPlayer(string method, string target, byte[] args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            if (NetManager == null) return this;
            Packet packet = new Packet
            {
                Type = PacketType.RPC,
                Target = target,
                Key = method,
                Data = args
            };
            NetManager.FirstPeer.Send(packet.Pack(), delMethod);
            return this;
        }
        /// <summary>
        /// Sends a Remote Procedure Call to the target player
        /// </summary>
        /// <typeparam name="T">The type of the arguments. Must be MemoryPackable</typeparam>
        /// <param name="method">The name of the method</param>
        /// <param name="target">The id of the target player</param>
        /// <param name="args">The arguments for the method. Must be MemoryPackable</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcPlayer<T>(string method, string target, T args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            byte[] bytes = MemoryPackSerializer.Serialize(args);
            return RpcPlayer(method, target, bytes, delMethod);
        }

        /// <summary>
        /// Sends a Remote Procedure Call to all players including the current player
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The MemoryPacked arguments for the method</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcAll(string method, byte[] args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            if (NetManager == null) return this;
            Packet packet = new Packet
            {
                Type = PacketType.RPC,
                Target = "all",
                Key = method,
                Data = args
            };
            NetManager.FirstPeer.Send(packet.Pack(), delMethod);
            return this;
        }
        /// <summary>
        /// Sends a Remote Procedure Call to all players including the current player
        /// </summary>
        /// <typeparam name="T">The type of the arguments. Must be MemoryPackable</typeparam>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The arguments for the method. Must be MemoryPackable</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcAll<T>(string method, T args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            byte[] bytes = MemoryPackSerializer.Serialize(args);
            return RpcAll(method, bytes, delMethod);
        }

        /// <summary>
        /// Sends a Remote Procedure Call to all players except the current player
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The MemoryPacked arguments for the method</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcOthers(string method, byte[] args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            if (NetManager == null) return this;
            Packet packet = new Packet
            {
                Type = PacketType.RPC,
                Target = "others",
                Key = method,
                Data = args
            };
            NetManager.FirstPeer.Send(packet.Pack(), delMethod);
            return this;
        }
        /// <summary>
        /// Sends a Remote Procedure Call to the server
        /// </summary>
        /// <typeparam name="T">The type of the arguments. Must be MemoryPackable</typeparam>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The arguments for the method. Must be MemoryPackable</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcOthers<T>(string method, T args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            byte[] bytes = MemoryPackSerializer.Serialize(args);
            return RpcOthers(method, bytes, delMethod);
        }

        /// <summary>
        /// Sends a Remote Procedure Call to the server
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The MemoryPacked arguments for the method</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcServer(string method, byte[] args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            if (NetManager == null) return this;
            Packet packet = new Packet
            {
                Type = PacketType.RPC,
                Target = "server",
                Key = method,
                Data = args
            };
            NetManager.FirstPeer.Send(packet.Pack(), delMethod);
            return this;
        }
        /// <summary>
        /// Sends a Remote Procedure Call to the server
        /// </summary>
        /// <typeparam name="T">The type of the arguments. Must be MemoryPackable</typeparam>
        /// <param name="method">The name of the method</param>
        /// <param name="args">The arguments for the method. Must be MemoryPackable</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        /// <returns>This client</returns>
        public BetterClient RpcServer<T>(string method, T args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            byte[] bytes = MemoryPackSerializer.Serialize(args);
            return RpcServer(method, bytes, delMethod);
        }
        /// <summary>
        /// A delegate for RPC actions on the client
        /// </summary>
        /// <param name="player">The id of the player that invoked the RPC</param>
        /// <param name="args">The MemoryPacked arguments</param>
        public delegate void ClientRpcAction(string player, byte[] args);

        /// <summary>
        /// Registers an action to be invoked when a <c>PacketType.SetState</c> packet with a specific key is received
        /// </summary>
        /// <param name="key">The key of the state</param>
        /// <param name="action">The action to be invoked</param>
        /// <returns>This client</returns>
        public BetterClient On(string key, Action<Packet> action)
        {
            RegisteredEvents[key] = action;
            return this;
        }
        /// <summary>
        /// Removes an action from the registered events
        /// </summary>
        /// <param name="key">The key of the state</param>
        /// <returns>This client</returns>
        public BetterClient Off(string key)
        {
            RegisteredEvents.Remove(key);
            return this;
        }

        private DateTime? _Ping = null;
        /// <summary>
        /// Pings the server and returns the delay. Only call once at a time
        /// </summary>
        /// <param name="timeout">The maximum time to wait for a response</param>
        /// <param name="method">The delivery method of LiteNetLib</param>
        /// <returns>The delay as a <c>TimeSpan</c></returns>
        public async Task<TimeSpan> PingServer(int timeout = 2000, DeliveryMethod method = DeliveryMethod.Unreliable)
        {
            if (NetManager == null) return TimeSpan.Zero;
            DateTime start = DateTime.Now;
            Packet packet = new Packet
            {
                Target = "server",
                Type = PacketType.Ping
            };
            NetManager.FirstPeer.Send(packet.Pack(), method);
            DateTime now = DateTime.Now;
            TimeSpan delay = now - await Task.Run(() =>
            {
                int i = 0;
                while (_Ping == null && i < timeout)
                {
                    Thread.Sleep(15);
                    i += 15;
                }
                if(_Ping == null) return now;
                else return _Ping.Value;
            });
            _Ping = null;
            return delay;
        }
        /// <summary>
        /// Sends a ping to a player and returns the delay. Only call once at a time
        /// </summary>
        /// <param name="playerId">The id of the target player</param>
        /// <param name="timeout">The maximum time to wait</param>
        /// <param name="method">The delivery method of LiteNetLib</param>
        /// <returns>The delay as a <c>TimeSpan</c></returns>
        public async Task<TimeSpan> PingPlayer(string playerId, int timeout = 2000, DeliveryMethod method = DeliveryMethod.Unreliable)
        {
            if (NetManager == null) return TimeSpan.Zero;
            DateTime start = DateTime.Now;
            Packet packet = new Packet
            {
                Target = playerId,
                Type = PacketType.Ping
            };
            NetManager.FirstPeer.Send(packet.Pack(), method);
            DateTime now = DateTime.Now;
            TimeSpan delay = now - await Task.Run(() =>
            {
                int i = 0;
                while (_Ping == null && i < timeout)
                {
                    Thread.Sleep(15);
                    i += 15;
                }
                if (_Ping == null) return now;
                else return _Ping.Value;
            });
            _Ping = null;
            return delay;
        }

        // Events

        /// <summary>
        /// Fired when the client is connected to the server. The string is the id assigned to this client by the server. You can also use <c>Client.Id</c> as it is assigned before this is called. The List is the list of all connected players exluding this player
        /// </summary>
        public event Action<string, List<string>>? Connected;
        /// <summary>
        /// Fluent version of <c>Connected</c>
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <returns>This client</returns>
        public BetterClient OnConnected(Action<string, List<string>> action)
        {
            Connected += action;
            return this;
        }

        /// <summary>
        /// Fired when the client is disconnected from the server
        /// </summary>
        public event Action<DisconnectInfo>? Disconnected;
        /// <summary>
        /// Fluent version of <c>Disconnected</c>
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <returns>This client</returns>
        public BetterClient OnDisconnected(Action<DisconnectInfo> action)
        {
            Disconnected += action;
            return this;
        }

        /// <summary>
        /// Fired when a player is connected to the server. The string is the id of the player
        /// </summary>
        public event Action<string>? PlayerConnected;
        /// <summary>
        /// Fluent version of <c>PlayerConnected</c>
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <returns>This client</returns>
        public BetterClient OnPlayerConnected(Action<string> action)
        {
            PlayerConnected += action;
            return this;
        }
        /// <summary>
        /// Fired when a player is disconnected from the server. The string is the id of the player
        /// </summary>
        public event Action<string>? PlayerDisconnected;
        /// <summary>
        /// Fluent version of <c>PlayerDisconnected</c>
        /// </summary>
        /// <param name="action"></param>
        /// <returns>This client</returns>
        public BetterClient OnPlayerDisconnected(Action<string> action)
        {
            PlayerDisconnected += action;
            return this;
        }
        /// <summary>
        /// Fired when a player is kicked from the server. The string is the reason of the kick
        /// </summary>
        public event Action<string>? Kicked;
        /// <summary>
        /// Fluent version of <c>Kicked</c>
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <returns>This client</returns>
        public BetterClient OnKicked(Action<string> action)
        {
            Kicked += action;
            return this;
        }
        /// <summary>
        /// Fired when a player is banned from the server. The string is the reason of the ban
        /// </summary>
        public event Action<string>? Banned;
        /// <summary>
        /// Fluent version of <c>Banned</c>
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <returns>This client</returns>
        public BetterClient OnBanned(Action<string> action)
        {
            Banned += action;
            return this;
        }
    }
}