using System;
using System.Collections.Generic;
using System.Net;
using BetterTogetherCore.Models;

namespace BetterTogetherCore.Transports
{
    /// <summary>
    /// The base class for all transports
    /// </summary>
    public abstract class Transport : IDisposable
    {
        /// <summary>
        /// Whether this transport will be used as a server or a client. Should be set in the constructor.
        /// </summary>
        public bool IsServer { get; protected set; }
        /// <summary>
        /// Used to start the server
        /// </summary>
        /// <param name="port">The port to host on</param>
        /// <returns><c>true</c> if the server started successfully, <c>false</c> otherwise</returns>
        public abstract bool StartServer(int port = 9050);
        /// <summary>
        /// Used to stop the server
        /// </summary>
        public abstract void StopServer();
        /// <summary>
        /// Used to start the client and attempt a connection to a server
        /// </summary>
        /// <param name="address">The address of the server</param>
        /// <param name="port">The port to use</param>
        /// <param name="extraData">Any extra data, memorypacked</param>
        /// <returns><c>true</c> if the connection was successful, <c>false</c> otherwise</returns>
        public abstract bool Connect(string address, int port, Dictionary<string, byte[]> extraData);
        /// <summary>
        /// Used to disconnect and stop the client
        /// </summary>
        public abstract void Disconnect();
        /// <summary>
        /// Used to disconnect a specific client from the server
        /// </summary>
        /// <param name="client"></param>
        /// <param name="reason"></param>
        public abstract void DisconnectClient(IPEndPoint client, string reason);
        /// <summary>
        /// Used on the client to send data to the server
        /// </summary>
        /// <param name="data">The memorypacked data to send</param>
        /// <param name="method">The delivery method</param>
        public abstract void Send(byte[] data, DeliveryMethod method);
        /// <summary>
        /// Used on the client to send data to the server
        /// </summary>
        /// <typeparam name="T">The type of the data to send. Must be MemoryPackable</typeparam>
        /// <param name="data">The data to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method</param>
        public abstract void Send<T>(T data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to a specific client
        /// </summary>
        /// <param name="target">The target client</param>
        /// <param name="data">The memorypacked data to send</param>
        /// <param name="method">The delivery method</param>
        public abstract void SendTo(IPEndPoint target, byte[] data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to a specific client
        /// </summary>
        /// <typeparam name="T">The type of the data to send. Must be MemoryPackable</typeparam>
        /// <param name="target">The target client</param>
        /// <param name="data">The data to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method</param>
        public abstract void SendTo<T>(IPEndPoint target, T data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to all clients
        /// </summary>
        /// <param name="data">The memorypacked data to send</param>
        /// <param name="method">The delivery method</param>
        public abstract void Broadcast(byte[] data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to all clients
        /// </summary>
        /// <typeparam name="T">The type of the data to send. Must be MemoryPackable</typeparam>
        /// <param name="data">The data to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method</param>
        public abstract void Broadcast<T>(T data, DeliveryMethod method);


        // Server events

        /// <summary>
        /// Delegate for when a client requests a connection to the server
        /// </summary>
        /// <param name="request"></param>
        public delegate void ServerClientConnectionRequestEvent(ConnectionRequest request);
        /// <summary>
        /// Event for when a client requests a connection to the server
        /// </summary>
        public event ServerClientConnectionRequestEvent? ServerClientConnectionRequest;
        /// <summary>
        /// Used to invoke the ServerClientConnectionRequest event
        /// </summary>
        /// <param name="request">The connection request</param>
        protected void OnServerClientConnectionRequest(ConnectionRequest request)
        {
            ServerClientConnectionRequest?.Invoke(request);
        }
        /// <summary>
        /// Delegate for when a client connects to the server
        /// </summary>
        /// <param name="client">The client's endpoint</param>
        public delegate void ServerClientConnectedEvent(IPEndPoint client);
        /// <summary>
        /// Event for when a client connects to the server
        /// </summary>
        public event ServerClientConnectedEvent? ServerClientConnected;
        /// <summary>
        /// Used to invoke the ServerClientConnected event
        /// </summary>
        /// <param name="client">The client's endpoint</param>
        protected void OnServerClientConnected(IPEndPoint client)
        {
            ServerClientConnected?.Invoke(client);
        }
        /// <summary>
        /// Delegate for when the server receives data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        /// <param name="method"></param>
        public delegate void ServerDataReceivedEvent(IPEndPoint sender, byte[] data, DeliveryMethod method);
        /// <summary>
        /// Event for when the server receives data
        /// </summary>
        public event ServerDataReceivedEvent? ServerDataReceived;
        /// <summary>
        /// Used to invoke the ServerDataReceived event
        /// </summary>
        /// <param name="sender">The sender's endpoint</param>
        /// <param name="data">The memorypacked data received</param>
        /// <param name="method">The delivery method</param>
        protected void OnServerDataReceived(IPEndPoint sender, byte[] data, DeliveryMethod method)
        {
            ServerDataReceived?.Invoke(sender, data, method);
        }
        /// <summary>
        /// Delegate for when a client disconnects from the server
        /// </summary>
        /// <param name="client">The client's endpoint</param>
        /// <param name="info">Information about the disconnection</param>
        public delegate void ServerClientDisconnectedEvent(IPEndPoint client, DisconnectInfo info);
        /// <summary>
        /// Event for when a client disconnects from the server
        /// </summary>
        public event ServerClientDisconnectedEvent? ServerClientDisconnected;
        /// <summary>
        /// Used to invoke the ServerClientDisconnected event
        /// </summary>
        /// <param name="client">The client's endpoint</param>
        /// <param name="info">Information about the disconnection</param>
        protected void OnServerClientDisconnected(IPEndPoint client, DisconnectInfo info)
        {
            ServerClientDisconnected?.Invoke(client, info);
        }

        // Client events

        /// <summary>
        /// Delegate for when the client receives data
        /// </summary>
        /// <param name="data">The memorypacked data received</param>
        /// <param name="method">The delivery method</param>
        public delegate void ClientDataReceivedEvent(byte[] data, DeliveryMethod method);
        /// <summary>
        /// Event for when the client receives data
        /// </summary>
        public event ClientDataReceivedEvent? ClientDataReceived;
        /// <summary>
        /// Used to invoke the ClientDataReceived event
        /// </summary>
        /// <param name="data">The memorypacked data received</param>
        /// <param name="method">The delivery method</param>
        protected void OnClientDataReceived(byte[] data, DeliveryMethod method)
        {
            ClientDataReceived?.Invoke(data, method);
        }
        /// <summary>
        /// Delegate for when the client gets disconnected from the server
        /// </summary>
        /// <param name="info">Information about the disconnection</param>
        public delegate void ClientDisconnectedEvent(DisconnectInfo info);
        /// <summary>
        /// Event for when the client gets disconnected from the server
        /// </summary>
        public event ClientDisconnectedEvent? ClientDisconnected;
        /// <summary>
        /// Used to invoke the ClientClientDisconnected event
        /// </summary>
        /// <param name="info">Information about the disconnection</param>
        protected void OnClientDisconnected(DisconnectInfo info)
        {
            ClientDisconnected?.Invoke(info);
        }

        /// <summary>
        /// Disposes of the transport
        /// </summary>
        public abstract void Dispose();
    }
}
