using BetterTogetherCore.Models;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace BetterTogetherCore.Transports.LiteNetLibTransport
{
    /// <summary>
    /// A client transport using LiteNetLib
    /// </summary>
    public partial class ClientLiteNetLibTransport : IClientTransport
    {
        /// <inheritdoc/>
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
        public event IClientTransport.DataReceivedEvent? DataReceived;
        /// <inheritdoc/>
        public event IClientTransport.DisconnectedEvent? Disconnected;

        /// <summary>
        /// Creates a new instance of the ClientLiteNetLibTransport
        /// </summary>
        public ClientLiteNetLibTransport()
        {
            _PollToken = new CancellationTokenSource();
            Listener = new EventBasedNetListener();
            NetManager = new NetManager(Listener);
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }
        /// <summary>
        /// Creates a new instance of the ClientLiteNetLibTransport with a logger
        /// </summary>
        /// <param name="logger">The logger object to use</param>
        public ClientLiteNetLibTransport(ILogger<ClientLiteNetLibTransport> logger) : this()
        {
            Logger = logger;
        }
        private void PollEvents()
        {
            while (true)
            {
                if (_PollToken?.IsCancellationRequested == true) break;
                NetManager?.PollEvents();
                Thread.Sleep(PollInterval);
            }
        }
        /// <inheritdoc/>
        public bool Connect(string address, int port, Dictionary<string, byte[]> extraData)
        {
            try
            {
                if (Logger != default) Connecting(Logger, address, port);
                NetManager.Start();
                ConnectionData connectionData = new ConnectionData("BetterTogether", extraData);
                NetDataWriter writer = new NetDataWriter();
                byte[] data = MemoryPackSerializer.Serialize(connectionData);
                writer.Put(data);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(address), port);
                if (NetManager.Connect(endPoint, writer) != null)
                {
                    _PollToken = new CancellationTokenSource();
                    Thread thread = new Thread(PollEvents);
                    thread.Start();
                    if(Logger != default) Connected(Logger, address, port);
                    return true;
                }
                else
                {
                    if(Logger != default) FailedToConnect(Logger, address, port);
                    NetManager.Stop();
                    return false;
                }
            }
            catch(Exception ex)
            {
                if(Logger != default) FailedToConnect(Logger, ex, address, port);
                NetManager.Stop();
                return false;
            }
        }
        /// <inheritdoc/>
        public void Disconnect()
        {
            _PollToken.Cancel();
            NetManager.DisconnectAll();
            NetManager.Stop();
            if(Logger != default) DisconnectedFromServer(Logger);
        }
        /// <inheritdoc/>
        public void Send(byte[] data, Models.DeliveryMethod method)
        {
            if(Logger != default) SendingData(Logger, data.Length);
            NetManager.FirstPeer.Send(data, (LiteNetLib.DeliveryMethod)method);
        }
        /// <inheritdoc/>
        public void Send<T>(T data, Models.DeliveryMethod method)
        {
            Send(MemoryPackSerializer.Serialize(data), method);
        }

        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, LiteNetLib.DeliveryMethod deliveryMethod)
        {
            if (Logger != default) ReceivedData(Logger, reader.AvailableBytes, peer.Address, peer.Port);
            byte[] data = reader.GetRemainingBytes();
            DataReceived?.Invoke(data, (Models.DeliveryMethod)deliveryMethod);
        }
        private void Listener_PeerDisconnectedEvent(NetPeer peer, LiteNetLib.DisconnectInfo disconnectInfo)
        {
            if(Logger != default) DisconnectedByServer(Logger, Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "");
            string message = disconnectInfo.AdditionalData.AvailableBytes > 0 ? disconnectInfo.AdditionalData.GetString() : "";
            Disconnected?.Invoke(new Models.DisconnectInfo(Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "", message));
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Listener.NetworkReceiveEvent -= Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent -= Listener_PeerDisconnectedEvent;
            _PollToken.Cancel();
            NetManager.Stop();
            if (Logger != default) Disposed(Logger);
        }

        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Connecting to server at {address}:{port}")]
        private static partial void Connecting(ILogger logger, string address, int port);
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connected to server at {address}:{port}")]
        private static partial void Connected(ILogger logger, string address, int port);
        [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to connect to server at {address}:{port}")]
        private static partial void FailedToConnect(ILogger logger, string address, int port);
        [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to connect to server at {address}:{port}")]
        private static partial void FailedToConnect(ILogger logger, Exception ex, string address, int port);
        [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Disconnected from server")]
        private static partial void DisconnectedFromServer(ILogger logger);
        [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Disconnected from server. Reason: {reason}")]
        private static partial void DisconnectedByServer(ILogger logger, string reason);
        [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Received {bytes} bytes of data from {address}:{port}")]
        private static partial void ReceivedData(ILogger logger, int bytes, IPAddress address, int port);
        [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Sending {bytes} bytes of data")]
        private static partial void SendingData(ILogger logger, int bytes);
        [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Disposed of ClientLiteNetLibTransport")]
        private static partial void Disposed(ILogger logger);
    }
}
