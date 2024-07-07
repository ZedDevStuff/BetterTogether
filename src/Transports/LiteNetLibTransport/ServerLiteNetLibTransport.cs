using BetterTogetherCore.Models;
using LiteNetLib.Utils;
using LiteNetLib;
using MemoryPack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BetterTogetherCore.Transports.LiteNetLibTransport
{
    /// <summary>
    /// A server transport using LiteNetLib
    /// </summary>
    public partial class ServerLiteNetLibTransport : IServerTransport
    {
        /// <summary>
        /// The logger
        /// </summary>
        public ILogger? Logger { get; private set; }
        /// <summary>
        /// The interval in milliseconds to poll for events
        /// </summary>
        public int PollInterval { get; set; } = 15;
        private Dictionary<IPEndPoint, NetPeer> _Peers = new Dictionary<IPEndPoint, NetPeer>();
        private CancellationTokenSource _PollToken;
        /// <summary>
        /// The Listener
        /// </summary>
        public EventBasedNetListener Listener { get; private set; }
        /// <summary>
        /// The NetManager
        /// </summary>
        public NetManager NetManager { get; private set; }
        /// <inheritdoc/>
        public event IServerTransport.ClientConnectionRequestEvent? ClientConnectionRequested;
        /// <inheritdoc/>
        public event IServerTransport.ClientConnectedEvent? ClientConnected;
        /// <inheritdoc/>
        public event IServerTransport.DataReceivedEvent? DataReceived;
        /// <inheritdoc/>
        public event IServerTransport.ClientDisconnectedEvent? ClientDisconnected;

        /// <summary>
        /// Creates a new instance of the ServerLiteNetLibTransport
        /// </summary>
        public ServerLiteNetLibTransport()
        {
            _PollToken = new CancellationTokenSource();
            Listener = new EventBasedNetListener();
            NetManager = new NetManager(Listener);
            Listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }
        /// <summary>
        /// Creates a new instance of the ServerLiteNetLibTransport with a logger
        /// </summary>
        /// <param name="logger">The logger object to use</param>
        public ServerLiteNetLibTransport(ILogger<ServerLiteNetLibTransport> logger) : this()
        {
            Logger = logger;
        }
        /// <inheritdoc/>
        public bool StartServer(int port = 9050)
        {
            try
            {
                if(Logger != default) ServerStarting(Logger, port);
                if(NetManager.Start(port))
                {
                    _PollToken = new CancellationTokenSource();
                    Task.Run(PollEvents);
                    Thread thread = new Thread(PollEvents);
                    thread.Start();
                    if(Logger != default) ServerStarted(Logger, port);
                    return true;
                }
                else
                {
                    if(Logger != default) ServerFailedToStart(Logger, port);
                    _PollToken = new CancellationTokenSource();
                    NetManager.Stop();
                    return false;
                }
            }
            catch (Exception ex)
            {
                if(Logger != default) ServerFailedToStart(Logger, port, ex);
                _PollToken = new CancellationTokenSource();
                NetManager.Stop();
                return false;
            }
        }
        /// <inheritdoc/>
        public void StopServer()
        {
            if(Logger != default) ServerStopping(Logger);
            _PollToken.Cancel();
            NetManager.Stop();
        }
        /// <inheritdoc/>
        public void DisconnectClient(IPEndPoint client, string reason)
        {
            if(Logger != default) DisconnectingClient(Logger, client, reason);
            _Peers[client]?.Disconnect(MemoryPackSerializer.Serialize(reason));
        }
        /// <inheritdoc/>
        public void SendTo(IPEndPoint target, byte[] data, Models.DeliveryMethod method)
        {
            if(_Peers.ContainsKey(target))
            {
                if(Logger != default) SendingDataTo(Logger, data.Length, target);
                _Peers[target]?.Send(data, (LiteNetLib.DeliveryMethod)method);
            }
            else
            {
                if(Logger != default) NoConnectionExists(Logger, target);
            }
        }
        /// <inheritdoc/>
        public void SendTo<T>(IPEndPoint target, T data, Models.DeliveryMethod method)
        {
            SendTo(target, MemoryPackSerializer.Serialize(data), method);
        }
        /// <inheritdoc/>
        public void Broadcast(byte[] data, Models.DeliveryMethod method)
        {
            Logger?.LogInformation("Broadcasting {0} bytes of data", data.Length);
            NetManager.SendToAll(data, (LiteNetLib.DeliveryMethod)method);
        }
        /// <inheritdoc/>
        public void Broadcast<T>(T data, Models.DeliveryMethod method)
        {
            Broadcast(MemoryPackSerializer.Serialize(data), method);
        }

        private void Listener_ConnectionRequestEvent(LiteNetLib.ConnectionRequest request)
        {
            if(Logger != default) ConnectionRequest(Logger, request.RemoteEndPoint);
            if(request.Data.AvailableBytes == 0)
            {
                if(Logger != default) NoDataInConnectionRequest(Logger, request.RemoteEndPoint);
                return;
            }
            byte[] bytes = request.Data.GetRemainingBytes();
            ConnectionData? data = MemoryPackSerializer.Deserialize<ConnectionData>(bytes);
            Models.ConnectionRequest bRequest = new Models.ConnectionRequest(request.RemoteEndPoint, data ?? new());
            if(data != null)
            {
                ClientConnectionRequested?.Invoke(bRequest);
                if(bRequest.Accepted)
                {
                    if(Logger != default) AcceptingConnectionRequest(Logger, request.RemoteEndPoint);
                    _Peers[bRequest.EndPoint] = request.Accept();
                }
                else
                {
                    NetDataWriter writer = new NetDataWriter();
                    writer.Put(MemoryPackSerializer.Serialize(bRequest.RejectionMessage));
                    if(Logger != default) RejectingConnectionRequest(Logger, request.RemoteEndPoint);
                    request.Reject(writer);
                }
            }
            else
            {
                if(Logger != default) InvalidConnectionRequestData(Logger, request.RemoteEndPoint);
            }

        }
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            if(Logger != default) PeerConnected(Logger, peer.Address, peer.Port);
            ClientConnected?.Invoke(new IPEndPoint(peer.Address, peer.Port));
        }
        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, LiteNetLib.DeliveryMethod deliveryMethod)
        {
            byte[] data = reader.GetRemainingBytes();
            DataReceived?.Invoke(new IPEndPoint(peer.Address, peer.Port), data, (Models.DeliveryMethod)deliveryMethod);
        }
        private void Listener_PeerDisconnectedEvent(NetPeer peer, LiteNetLib.DisconnectInfo disconnectInfo)
        {
            string message = disconnectInfo.AdditionalData.AvailableBytes > 0 ? disconnectInfo.AdditionalData.GetString() : "";
            if(Logger != default)
            {
                if(message != "") PeerDisconnected(Logger, peer.Address, peer.Port, message);
                else PeerDisconnected(Logger, peer.Address, peer.Port, Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "");
            }
            ClientDisconnected?.Invoke(new IPEndPoint(peer.Address, peer.Port), new Models.DisconnectInfo(Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "", message));
        }

        private void PollEvents()
        {
            while (true)
            {
                if(_PollToken?.IsCancellationRequested == true) break;
                NetManager?.PollEvents();
                Thread.Sleep(PollInterval);
            }
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Listener.ConnectionRequestEvent -= Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent -= Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent -= Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent -= Listener_PeerDisconnectedEvent;
            _PollToken.Cancel();
            NetManager.Stop();
            if (Logger != default) Disposed(Logger);
        }

        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Starting server on port {port}")]
        private static partial void ServerStarting(ILogger logger, int port);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Server started on port {port}")]
        private static partial void ServerStarted(ILogger logger, int port);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Stopping server")]
        private static partial void ServerStopping(ILogger logger);
        [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Failed to start server on port {port}")]
        private static partial void ServerFailedToStart(ILogger logger, int port, Exception? ex = null);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Disconnecting client {client} with reason {reason}")]
        private static partial void DisconnectingClient(ILogger logger, IPEndPoint client, string reason);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Sending {dataLength} bytes of data to {target}")]
        private static partial void SendingDataTo(ILogger logger, int dataLength, IPEndPoint target);
        [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Tried to send data to {target} but no connection exists")]
        private static partial void NoConnectionExists(ILogger logger, IPEndPoint target);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Broadcasting {dataLength} bytes of data")]
        private static partial void BroadcastingData(ILogger logger, int dataLength);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Connection request from {endPoint}")]
        private static partial void ConnectionRequest(ILogger logger, IPEndPoint endPoint);
        [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "No data in connection request from {endPoint}, ignoring")]
        private static partial void NoDataInConnectionRequest(ILogger logger, IPEndPoint endPoint);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Accepting connection request from {endPoint}")]
        private static partial void AcceptingConnectionRequest(ILogger logger, IPEndPoint endPoint);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Rejecting connection request from {endPoint}")]
        private static partial void RejectingConnectionRequest(ILogger logger, IPEndPoint endPoint);
        [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Invalid connection request data from {endPoint}, ignoring")]
        private static partial void InvalidConnectionRequestData(ILogger logger, IPEndPoint endPoint);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Peer connected: {address}:{port}")]
        private static partial void PeerConnected(ILogger logger, IPAddress address, int port);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Received {dataLength} bytes of data from {address}:{port}")]
        private static partial void ReceivedData(ILogger logger, int dataLength, IPAddress address, int port);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Peer disconnected: {address}:{port}. Reason: {reason}")]
        private static partial void PeerDisconnected(ILogger logger, IPAddress address, int port, string reason);
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Disposed of ServerLiteNetLibTransport")]
        private static partial void Disposed(ILogger logger);
    }
}
