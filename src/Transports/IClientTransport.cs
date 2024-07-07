using BetterTogetherCore.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;

namespace BetterTogetherCore.Transports
{
    /// <summary>
    /// A client transport
    /// </summary>
    public interface IClientTransport : IDisposable
    {
        /// <summary>
        /// The logger used by this transport
        /// </summary>
        public ILogger? Logger { get; }
        /// <summary>
        /// Used to start the client and attempt a connection to a server
        /// </summary>
        /// <param name="address">The address of the server</param>
        /// <param name="port">The port to use</param>
        /// <param name="extraData">Any extra data, memorypacked</param>
        /// <returns><c>true</c> if the connection was successful, <c>false</c> otherwise</returns>
        public bool Connect(string address, int port, Dictionary<string, byte[]> extraData);
        /// <summary>
        /// Used to disconnect and stop the client
        /// </summary>
        public void Disconnect();
        /// <summary>
        /// Used on the client to send data to the server
        /// </summary>
        /// <param name="data">The memorypacked data to send</param>
        /// <param name="method">The delivery method</param>
        public void Send(byte[] data, DeliveryMethod method);
        /// <summary>
        /// Used on the client to send data to the server
        /// </summary>
        /// <typeparam name="T">The type of the data to send. Must be MemoryPackable</typeparam>
        /// <param name="data">The data to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method</param>
        public void Send<T>(T data, DeliveryMethod method);
        /// <summary>
        /// Delegate for when the client receives data
        /// </summary>
        /// <param name="data">The memorypacked data received</param>
        /// <param name="method">The delivery method</param>
        public delegate void DataReceivedEvent(byte[] data, DeliveryMethod method);
        /// <summary>
        /// Event for when the client receives data
        /// </summary>
        public event DataReceivedEvent? DataReceived;
        /// <summary>
        /// Delegate for when the client gets disconnected from the server
        /// </summary>
        /// <param name="info">Information about the disconnection</param>
        public delegate void DisconnectedEvent(DisconnectInfo info);
        /// <summary>
        /// Event for when the client gets disconnected from the server
        /// </summary>
        public event DisconnectedEvent? Disconnected;
    }
}
