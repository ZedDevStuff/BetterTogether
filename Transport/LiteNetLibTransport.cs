using System;
using System.Collections.Generic;
using System.Text;
using LiteNetLib;

namespace BetterTogetherCore.Transport
{
    internal class LiteNetLibTransport : Transport
    {
        public int PollInterval { get; set; } = 15;
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
            Listener = new EventBasedNetListener();
            NetManager = new NetManager(Listener);
            Listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
            Listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            Listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            Listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }
        private void PollEvents()
        {
            
        }

    }
}
