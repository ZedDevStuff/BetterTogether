using MemoryPack;
using System.Collections.Generic;

namespace BetterTogetherCore.Models
{
    /// <summary>
    /// This class is sent to the server to establish a connection along with initial states
    /// </summary>
    [MemoryPackable]
    public partial record ConnectionData
    {
        /// <summary>
        /// The key of the connection
        /// </summary>
        public string Key { get; set; } = "BetterTogether";
        /// <summary>
        /// Extra data
        /// </summary>
        public Dictionary<string, byte[]> ExtraData { get; set; } = new Dictionary<string, byte[]>();
        /// <summary>
        /// Constructor 
        /// </summary>
        public ConnectionData() { }
        /// <summary>
        /// Constructor with key
        /// </summary>
        /// <param name="key">The key for the connection. Will get rejected if it's not the same as the server's key. It should always be "BetterTogether" for this library</param>
        public ConnectionData(string key)
        {
            Key = key;
        }
        /// <summary>
        /// Constructor with key and initial states
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="extraData">Extra data</param>
        [MemoryPackConstructor]
        public ConnectionData(string key, Dictionary<string, byte[]> extraData)
        {
            Key = key;
            ExtraData = extraData;
        }
        private ConnectionData(string key, byte[] _extraData)
        {
            Key = key;
            ExtraData = MemoryPackSerializer.Deserialize<Dictionary<string, byte[]>>(_extraData) ?? new(); 
        }
    }
}
