using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using MemoryPack;

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
        private ConcurrentDictionary<string, byte[]> States { get; set; } = new();
        private Dictionary<string, Action<byte[]>> RegisteredRPCs { get; set; } = new Dictionary<string, Action<byte[]>>();

        /// <summary>
        /// Creates a new BetterClient
        /// </summary>
        public BetterClient()
        {
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
        /// Connects the client to the target server
        /// </summary>
        /// <param name="host">The address of the server</param>
        /// <param name="port">The port of the server</param>
        public void Connect(string host, int port = 9050)
        {
            NetManager = new NetManager(Listener);
            NetManager.Start();
            try
            {
                NetManager.Connect(host, port, "BetterTogether");
            }
            catch
            {
                NetManager.Stop();
                NetManager = null;
                return;
            }
            Thread thread = new Thread(PollEvents);
            thread.Start();
        }
        /// <summary>
        /// Disconnects the client from the server
        /// </summary>
        public void Disconnect()
        {
            if(NetManager == null) return;
            Id = "";
            NetManager.DisconnectPeer(NetManager.FirstPeer);
            NetManager?.Stop();
            NetManager = null;
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
                            peer.Send(MemoryPackSerializer.Serialize(pong), deliveryMethod);
                        }
                        break;
                    case PacketType.SetState:
                        if (packet.Key.FastStartsWith(Id)) return;
                        States[packet.Key] = packet.Data;
                        break;
                    case PacketType.Init:
                        var states = packet.GetData<ConcurrentDictionary<string, byte[]>>();
                        if(states != null) States = states;
                        break;
                    case PacketType.RPC:
                        HandleRPC(packet.Key, packet.Data);
                        break;
                    case PacketType.SelfConnected:
                        List<string>? list = packet.GetData<List<string>>();
                        if (list != null)
                        {
                            Id = list[0];
                            _Players = list;
                            list.Remove(Id);
                            OnConnected?.Invoke(Id, list);
                        }
                        break;
                    case PacketType.PeerConnected:
                        string connectedId = Encoding.UTF8.GetString(packet.Data);
                        _Players.Add(connectedId);
                        OnPlayerConnected?.Invoke(connectedId);
                        break;
                    case PacketType.PeerDisconnected:
                        string disconnectedId = Encoding.UTF8.GetString(packet.Data);
                        _Players.Remove(disconnectedId);
                        OnPlayerDisconnected?.Invoke(disconnectedId);
                        break;
                    case PacketType.Kick:
                        OnKicked?.Invoke(Encoding.UTF8.GetString(packet.Data));
                        break;
                    case PacketType.Ban:
                        OnBanned?.Invoke(Encoding.UTF8.GetString(packet.Data));
                        break;
                    default:
                        break;
                }
            }
        }

        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            OnDisconnected?.Invoke(disconnectInfo);
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
            States[key] = data;
            Packet packet = new Packet
            {
                Type = PacketType.SetState,
                Key = key,
                Data = data
            };
            byte[] bytes = MemoryPackSerializer.Serialize(packet);
            NetManager.FirstPeer.Send(bytes, method);
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
            States[Id + key] = data;
            Packet packet = new Packet
            {
                Type = PacketType.SetState,
                Key = Id + key,
                Data = data
            };
            byte[] bytes = MemoryPackSerializer.Serialize(packet);
            NetManager.FirstPeer.Send(bytes, method);
        }
        
        /// <summary>
        /// Sends a state object to the server
        /// </summary>
        /// <typeparam name="T">The type of the object. Must be MemoryPackable</typeparam>
        /// <param name="key">The name of the state to set</param>
        /// <param name="data">The object to send. Must be MemoryPackable</param>
        /// <param name="method"></param>
        public void SetState<T>(string key, T data, DeliveryMethod method = DeliveryMethod.ReliableUnordered)
        {
            if (NetManager == null) return;
            byte[] bytes = MemoryPackSerializer.Serialize(data);
            States[key] = bytes;
            Packet packet = new Packet
            {
                Type = PacketType.SetState,
                Key = key,
                Data = bytes
            };
            byte[] packetBytes = MemoryPackSerializer.Serialize(packet);
            NetManager.FirstPeer.Send(packetBytes, method);
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
            if (NetManager == null) return;
            byte[] bytes = MemoryPackSerializer.Serialize(data);
            States[Id + key] = bytes;
            Packet packet = new Packet
            {
                Type = PacketType.SetState,
                Key = Id + key,
                Data = bytes
            };
            byte[] packetBytes = MemoryPackSerializer.Serialize(packet);
            NetManager.FirstPeer.Send(packetBytes, method);
        }
        
        /// <summary>
        /// Gets the latest state of a key available on this client
        /// </summary>
        /// <typeparam name="T">The expected type of the object. Must be MemoryPackable</typeparam>
        /// <param name="key">The name of the state object</param>
        /// <returns>The deserialized object or the default value of the expected type</returns>
        public T? GetState<T>(string key)
        {
            if (States.ContainsKey(key))
            {
                return MemoryPackSerializer.Deserialize<T>(States[key]);
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
            if (States.ContainsKey(finalKey))
            {
                return MemoryPackSerializer.Deserialize<T>(States[finalKey]);
            }
            return default(T);
        }
        
        /// <summary>
        /// Registers a Remote Procedure Call with a method name and an action to invoke
        /// </summary>
        /// <param name="method">The name of the method</param>
        /// <param name="action">The method</param>
        public void RegisterRPC(string method, Action<byte[]> action)
        {
            RegisteredRPCs[method] = action;
        }
        private void HandleRPC(string method, byte[] args)
        {
            if(RegisteredRPCs.ContainsKey(method))
            {
                RegisteredRPCs[method](args);
            }
        }
        
        /// <summary>
        /// Sends a Remote Procedure Call to the server to be dispatched to the target player
        /// </summary>
        /// <param name="method">The name of the method to invoke</param>
        /// <param name="target">The id of the target player</param>
        /// <param name="args">The MemoryPacked arguments for the method</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        public void RPC(string method, string target, byte[] args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        {
            if (NetManager == null) return;
            Packet packet = new Packet
            {
                Type = PacketType.RPC,
                Target = target,
                Key = method,
                Data = args
            };
            byte[] bytes = MemoryPackSerializer.Serialize(packet);
            NetManager.FirstPeer.Send(bytes, delMethod);
        }

        /// <summary>
        /// Sends a Remote Procedure Call to the server to be dispatched to the target player
        /// </summary>
        /// <param name="method">The name of the method to invoke</param>
        /// <param name="target">The id of the target player</param>
        /// <param name="args">The arguments object for the method. must be MemoryPackable</param>
        /// <param name="delMethod">The delivery method of LiteNetLib</param>
        public void RPC(string method, string target, object args, DeliveryMethod delMethod = DeliveryMethod.ReliableOrdered)
        { 
            byte[] bytes = MemoryPackSerializer.Serialize(args);
            RPC(method, target, bytes, delMethod);
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
            byte[] bytes = MemoryPackSerializer.Serialize(packet);
            NetManager.FirstPeer.Send(bytes, method);
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
            byte[] bytes = MemoryPackSerializer.Serialize(packet);
            NetManager.FirstPeer.Send(bytes, method);
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
        public event Action<string, List<string>>? OnConnected;

        /// <summary>
        /// Fired when the client is disconnected from the server
        /// </summary>
        public event Action<DisconnectInfo>? OnDisconnected;

        /// <summary>
        /// Fired when a player is connected to the server. The string is the id of the player
        /// </summary>
        public event Action<string>? OnPlayerConnected;
        /// <summary>
        /// Fired when a player is disconnected from the server. The string is the id of the player
        /// </summary>
        public event Action<string>? OnPlayerDisconnected;
        /// <summary>
        /// Fired when a player is kicked from the server. The string is the reason of the kick
        /// </summary>
        public event Action<string>? OnKicked;
        /// <summary>
        /// Fired when a player is banned from the server. The string is the reason of the ban
        /// </summary>
        public event Action<string>? OnBanned;
    }
}