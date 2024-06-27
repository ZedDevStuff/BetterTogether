using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;

namespace BetterTogetherCore.Transport
{
    internal class LiteNetLibTransport : Transport
    {
        public int PollInterval { get; set; } = 15;
        private Dictionary<IPAddress, NetPeer> _Peers = new Dictionary<IPAddress, NetPeer>();
        private CancellationTokenSource _PollToken;
        /// <summary>
        /// The Listener
        /// </summary>
        public EventBasedNetListener Listener { get; private set; }
        /// <summary>
        /// The NetManager
        /// </summary>
        public NetManager NetManager { get; private set; }

        public LiteNetLibTransport(bool isServer)
        {
            _PollToken = new CancellationTokenSource();
            IsServer = isServer;
            Listener = new EventBasedNetListener();
            NetManager = new NetManager(Listener);
            Listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }

        public override bool StartServer(int port = 9050)
        {
            try
            {
                if (NetManager.Start(port))
                {
                    _PollToken = new CancellationTokenSource();
                    Thread thread = new Thread(PollEvents);
                    thread.Start();
                    return true;
                }
                else
                {
                    NetManager.Stop();
                    return false;
                }
            }
            catch
            {
                NetManager.Stop();
                return false;
            }
        }
        public override void StopServer()
        {
            _PollToken.Cancel();
            NetManager.Stop();
        }
        public override bool Connect(string address, int port, Dictionary<string, byte[]> extraData)
        {
            try
            {
                NetManager.Start();
                ConnectionData connectionData = new ConnectionData("BetterTogether", extraData);
                NetDataWriter writer = new NetDataWriter();
                byte[] data = MemoryPackSerializer.Serialize(connectionData);
                Console.WriteLine(data.Length);
                writer.Put(data);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(address), port);
                if (NetManager.Connect(endPoint, writer) != null)
                {
                    _PollToken = new CancellationTokenSource();
                    Thread thread = new Thread(PollEvents);
                    thread.Start();
                    return true;
                }
                else
                {
                    NetManager.Stop();
                    return false;
                }
            }
            catch
            {
                NetManager.Stop();
                return false;
            }
        }
        public override void Disconnect()
        {
            _PollToken.Cancel();
            NetManager.DisconnectPeer(NetManager.FirstPeer);
            NetManager.Stop();
        }
        public override void Send(byte[] data, DeliveryMethod method)
        {
            NetManager.FirstPeer.Send(data, (LiteNetLib.DeliveryMethod)method);
        }
        public override void Send<T>(T data, DeliveryMethod method)
        {
            Send(MemoryPackSerializer.Serialize(data), method);
        }
        public override void SendTo(IPAddress target, byte[] data, DeliveryMethod method)
        {
            if (_Peers.ContainsKey(target))
            {
                _Peers[target].Send(data, (LiteNetLib.DeliveryMethod)method);
            }
        }
        public override void SendTo<T>(IPAddress target, T data, DeliveryMethod method)
        {
            SendTo(target, MemoryPackSerializer.Serialize(data), method);
        }
        public override void Broadcast(byte[] data, DeliveryMethod method)
        {
            NetManager.SendToAll(data, (LiteNetLib.DeliveryMethod)method);
        }
        public override void Broadcast<T>(T data, DeliveryMethod method)
        {
            Broadcast(MemoryPackSerializer.Serialize(data), method);
        }

        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {
            if (request.Data.UserDataSize == 0) return;
            byte[] bytes = request.Data.GetRemainingBytes();
            ConnectionData data = MemoryPackSerializer.Deserialize<ConnectionData>(bytes);
            OnServerClientConnectionRequest(request.RemoteEndPoint.Address, data);
        }
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            OnServerClientConnected(peer.Address);
        }
        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, LiteNetLib.DeliveryMethod deliveryMethod)
        {
            byte[] data = reader.GetRemainingBytes();
            if(IsServer) OnServerDataReceived(peer.Address, data, (DeliveryMethod)deliveryMethod);
            else OnClientDataReceived(data, (DeliveryMethod)deliveryMethod);
        }
        private void Listener_PeerDisconnectedEvent(NetPeer peer, LiteNetLib.DisconnectInfo disconnectInfo)
        {
            string message = disconnectInfo.AdditionalData.AvailableBytes > 0 ? disconnectInfo.AdditionalData.GetString() : "";
            if(IsServer) OnServerClientDisconnected(peer.Address, new DisconnectInfo(Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "", message));
            else OnClientDisconnected(new DisconnectInfo(Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "", message));
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
    }
}
