using System.Net;
using MemoryPack;

namespace BetterTogetherCore.Models
{
    [MemoryPackable]
    public partial class ConnectionRequest
    {
        /// <summary>
        /// The endpoint of the connection request
        /// </summary>
        [MemoryPackIgnore]
        public IPEndPoint EndPoint;

        [MemoryPackInclude, MemoryPackOrder(0)]
        private string _Address = "";
        [MemoryPackInclude, MemoryPackOrder(1)]
        private int _Port = 0;
        /// <summary>
        /// The connection data of the connection request
        /// </summary>
        [MemoryPackIgnore]
        public ConnectionData ConnectionData { get; set; }
        [MemoryPackInclude, MemoryPackOrder(2)]
        private byte[] _ConnectionData { get; set; } = new byte[0];
        /// <summary>
        /// Whether the connection request was accepted
        /// </summary>
        public bool Accepted { get; private set; } = false;
        /// <summary>
        /// The rejection message if the connection request was rejected
        /// </summary>
        public string RejectionMessage { get; private set; } = "";

        public ConnectionRequest() {}
        public ConnectionRequest(IPEndPoint endPoint, ConnectionData connectionData)
        {
            EndPoint = endPoint;
            ConnectionData = connectionData;
        }
        [MemoryPackConstructor]
        private ConnectionRequest(string _address, int _port, byte[] _connectionData)
        {
            EndPoint = new IPEndPoint(IPAddress.Parse(_address), _port);
            ConnectionData = MemoryPackSerializer.Deserialize<Models.ConnectionData>(_connectionData) ?? new();
        }

        public void Accept()
        {
            Accepted = true;
        }
        public void Reject(string message)
        {
            Accepted = false;
            RejectionMessage = message;
        }
        [MemoryPackOnSerializing]
        private void Prep()
        {
            _Address = EndPoint.Address.ToString();
            _Port = EndPoint.Port;
            _ConnectionData = MemoryPackSerializer.Serialize(ConnectionData);
        }
    }
}
