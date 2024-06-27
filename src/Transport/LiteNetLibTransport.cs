using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using BetterTogetherCore.Models;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;

namespace BetterTogetherCore.Transports
{
    internal class LiteNetLibTransport : Transport
    {
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
        public override void DisconnectClient(IPEndPoint client, string reason)
        {
            _Peers[client]?.Disconnect(MemoryPackSerializer.Serialize(reason));
        }
        public override void Send(byte[] data, Models.DeliveryMethod method)
        {
            NetManager.FirstPeer.Send(data, (LiteNetLib.DeliveryMethod)method);
        }
        public override void Send<T>(T data, Models.DeliveryMethod method)
        {
            Send(MemoryPackSerializer.Serialize(data), method);
        }
        public override void SendTo(IPEndPoint target, byte[] data, Models.DeliveryMethod method)
        {
            if (_Peers.ContainsKey(target))
            {
                Debug.Assert(_Peers[target].ConnectionState == ConnectionState.Connected);
                _Peers[target]?.Send(data, (LiteNetLib.DeliveryMethod)method);
            }
        }
        public override void SendTo<T>(IPEndPoint target, T data, Models.DeliveryMethod method)
        {
            SendTo(target, MemoryPackSerializer.Serialize(data), method);
        }
        public override void Broadcast(byte[] data, Models.DeliveryMethod method)
        {
            NetManager.SendToAll(data, (LiteNetLib.DeliveryMethod)method);
        }
        public override void Broadcast<T>(T data, Models.DeliveryMethod method)
        {
            Broadcast(MemoryPackSerializer.Serialize(data), method);
        }

        private void Listener_ConnectionRequestEvent(LiteNetLib.ConnectionRequest request)
        {
            if (request.Data.AvailableBytes == 0) return;
            byte[] bytes = request.Data.GetRemainingBytes();
            ConnectionData? data = MemoryPackSerializer.Deserialize<ConnectionData>(bytes);
            Models.ConnectionRequest bRequest = new Models.ConnectionRequest(request.RemoteEndPoint, data ?? new());
            if (data != null)
            {
                OnServerClientConnectionRequest(bRequest);
                if (bRequest.Accepted)
                {
                    _Peers[bRequest.EndPoint] = request.Accept();
                }
                else
                {
                    NetDataWriter writer = new NetDataWriter();
                    writer.Put(MemoryPackSerializer.Serialize(bRequest.RejectionMessage));
                    request.Reject(writer);
                }
            }

        }
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            OnServerClientConnected(new IPEndPoint(peer.Address, peer.Port));
        }
        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, LiteNetLib.DeliveryMethod deliveryMethod)
        {
            byte[] data = reader.GetRemainingBytes();
            if(IsServer) OnServerDataReceived(new IPEndPoint(peer.Address, peer.Port), data, (Models.DeliveryMethod)deliveryMethod);
            else OnClientDataReceived(data, (Models.DeliveryMethod)deliveryMethod);
        }
        private void Listener_PeerDisconnectedEvent(NetPeer peer, LiteNetLib.DisconnectInfo disconnectInfo)
        {
            string message = disconnectInfo.AdditionalData.AvailableBytes > 0 ? disconnectInfo.AdditionalData.GetString() : "";
            if(IsServer) OnServerClientDisconnected(new IPEndPoint(peer.Address, peer.Port), new Models.DisconnectInfo(Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "", message));
            else OnClientDisconnected(new Models.DisconnectInfo(Enum.GetName(typeof(DisconnectReason), disconnectInfo.Reason) ?? "", message));
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
        public override void Dispose()
        {
            Listener.ConnectionRequestEvent -= Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent -= Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent -= Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent -= Listener_PeerDisconnectedEvent;
            _PollToken.Cancel();
            NetManager.Stop();
        }
    }
}
