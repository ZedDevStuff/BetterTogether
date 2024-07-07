using BetterTogetherCore.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace BetterTogetherCore.Transports
{
    /// <summary>
    /// A server transport
    /// </summary>
    public interface IServerTransport : IDisposable
    {
        /// <summary>
        /// The logger used by this transport
        /// </summary>
        public ILogger? Logger { get; }
        /// <summary>
        /// Used to start the server
        /// </summary>
        /// <param name="port">The port to host on</param>
        /// <returns><c>true</c> if the server started successfully, <c>false</c> otherwise</returns>
        public bool StartServer(int port = 9050);
        /// <summary>
        /// Used to stop the server
        /// </summary>
        public void StopServer();
        /// <summary>
        /// Used to disconnect a specific client from the server
        /// </summary>
        /// <param name="client">The client to disconnect</param>
        /// <param name="reason">The reason to send</param>
        public void DisconnectClient(IPEndPoint client, string reason);
        /// <summary>
        /// Used on the server to send data to a specific client
        /// </summary>
        /// <param name="target">The target client</param>
        /// <param name="data">The memorypacked data to send</param>
        /// <param name="method">The delivery method</param>
        public void SendTo(IPEndPoint target, byte[] data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to a specific client
        /// </summary>
        /// <typeparam name="T">The type of the data to send. Must be MemoryPackable</typeparam>
        /// <param name="target">The target client</param>
        /// <param name="data">The data to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method</param>
        public void SendTo<T>(IPEndPoint target, T data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to all clients
        /// </summary>
        /// <param name="data">The memorypacked data to send</param>
        /// <param name="method">The delivery method</param>
        public void Broadcast(byte[] data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to all clients
        /// </summary>
        /// <typeparam name="T">The type of the data to send. Must be MemoryPackable</typeparam>
        /// <param name="data">The data to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method</param>
        public void Broadcast<T>(T data, DeliveryMethod method);


        // Server events

        /// <summary>
        /// Delegate for when a client requests a connection to the server
        /// </summary>
        /// <param name="request"></param>
        public delegate void ClientConnectionRequestEvent(ConnectionRequest request);
        /// <summary>
        /// Event for when a client requests a connection to the server
        /// </summary>
        public event ClientConnectionRequestEvent? ClientConnectionRequested;
        /// <summary>
        /// Delegate for when a client connects to the server
        /// </summary>
        /// <param name="client">The client's endpoint</param>
        public delegate void ClientConnectedEvent(IPEndPoint client);
        /// <summary>
        /// Event for when a client connects to the server
        /// </summary>
        public event ClientConnectedEvent? ClientConnected;
        /// <summary>
        /// Delegate for when the server receives data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        /// <param name="method"></param>
        public delegate void DataReceivedEvent(IPEndPoint sender, byte[] data, DeliveryMethod method);
        /// <summary>
        /// Event for when the server receives data
        /// </summary>
        public event DataReceivedEvent? DataReceived;
        /// <summary>
        /// Delegate for when a client disconnects from the server
        /// </summary>
        /// <param name="client">The client's endpoint</param>
        /// <param name="info">Information about the disconnection</param>
        public delegate void ClientDisconnectedEvent(IPEndPoint client, DisconnectInfo info);
        /// <summary>
        /// Event for when a client disconnects from the server
        /// </summary>
        public event ClientDisconnectedEvent? ClientDisconnected;
    }
}
