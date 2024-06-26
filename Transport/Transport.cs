using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BetterTogetherCore.Transport
{
    public abstract class Transport
    {
        public bool IsServer { get; private set; } = false;
        public abstract void Start();
        public abstract void Stop();
        public abstract void Connect(string address, int port);
        public abstract void SendTo(object target, string message);
        public abstract void SendTo<T>(object target, T data);
        public abstract void SendToAll(string message);
        public abstract void SendToAll<T>(T data);

        public delegate void ServerClientConnectionRequestEvent(IPAddress client, byte[] extraData);
        public event ServerClientConnectionRequestEvent? ServerClientConnectionRequest;
        public delegate void ServerClientConnectedEvent(IPAddress client, byte[] extraData);
        public event ServerClientConnectedEvent? ServerClientConnected;
        public delegate void ServerMessageReceivedEvent(IPAddress sender, byte[] data, DeliveryMethod method);
        public event ServerMessageReceivedEvent? MessageReceived;
        public delegate void ClientDisconnectedEvent(IPAddress client, byte[] extraData);
        public event ClientDisconnectedEvent? ClientDisconnected;

        /// <summary>
        /// Those are based on LiteNetLib's DeliveryMethods. If your transport does not support these, use Unsupported in any case.
        /// </summary>
        public enum DeliveryMethod
        {
            /// <summary>
            /// Reliable. Packets won't be dropped, won't be duplicated, can arrive without order.
            /// </summary>
            ReliableUnordered,
            /// <summary>
            /// 
            /// </summary>
            Sequenced,
            /// <summary>
            /// 
            /// </summary>
            ReliableOrdered,
            /// <summary>
            /// 
            /// </summary>
            ReliableSequenced,
            /// <summary>
            /// 
            /// </summary>
            Unreliable,
            /// <summary>
            /// Used for transports that do not support delivery methods
            /// </summary>
            Unsupported,
        }
    }
}
