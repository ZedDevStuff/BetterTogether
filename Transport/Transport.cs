using System.Collections.Generic;
using System.Net;

namespace BetterTogetherCore.Transport
{
    public abstract class Transport
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
        public abstract void SendTo(IPAddress target, byte[] data, DeliveryMethod method);
        /// <summary>
        /// Used on the server to send data to a specific client
        /// </summary>
        /// <typeparam name="T">The type of the data to send. Must be MemoryPackable</typeparam>
        /// <param name="target">The target client</param>
        /// <param name="data">The data to send. Must be MemoryPackable</param>
        /// <param name="method">The delivery method</param>
        public abstract void SendTo<T>(IPAddress target, T data, DeliveryMethod method);
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
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="data"></param>
        public delegate void ServerClientConnectionRequestEvent(IPAddress client, ConnectionData data);
        /// <summary>
        /// 
        /// </summary>
        public event ServerClientConnectionRequestEvent? ServerClientConnectionRequest;
        /// <summary>
        /// Used to invoke the ServerClientConnectionRequest event
        /// </summary>
        /// <param name="client">The client's IP address</param>
        /// <param name="data">The connection data</param>
        protected void OnServerClientConnectionRequest(IPAddress client, ConnectionData data)
        {
            ServerClientConnectionRequest?.Invoke(client, data);
        }
        public delegate void ServerClientConnectedEvent(IPAddress client);
        public event ServerClientConnectedEvent? ServerClientConnected;
        protected void OnServerClientConnected(IPAddress client)
        {
            ServerClientConnected?.Invoke(client);
        }
        public delegate void ServerDataReceivedEvent(IPAddress sender, byte[] data, DeliveryMethod method);
        public event ServerDataReceivedEvent? ServerDataReceived;
        protected void OnServerDataReceived(IPAddress sender, byte[] data, DeliveryMethod method)
        {
            ServerDataReceived?.Invoke(sender, data, method);
        }
        public delegate void ServerClientDisconnectedEvent(IPAddress client, DisconnectInfo info);
        public event ServerClientDisconnectedEvent? ServerClientDisconnected;
        protected void OnServerClientDisconnected(IPAddress client, DisconnectInfo info)
        {
            ServerClientDisconnected?.Invoke(client, info);
        }

        // Client events

        public delegate void ClientDataReceivedEvent(byte[] data, DeliveryMethod method);
        public event ClientDataReceivedEvent? ClientDataReceived;
        protected void OnClientDataReceived(byte[] data, DeliveryMethod method)
        {
            ClientDataReceived?.Invoke(data, method);
        }
        public delegate void ClientDisconnectedEvent(DisconnectInfo info);
        public event ClientDisconnectedEvent? ClientDisconnected;
        protected void OnClientDisconnected(DisconnectInfo info)
        {
            ClientDisconnected?.Invoke(info);
        }
    }
}
