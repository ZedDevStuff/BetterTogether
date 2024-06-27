using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text;

namespace BetterTogetherCore.Transport
{
    /// <summary>
    /// Tis struct is sent to the server to establish a connection along with initial states
    /// </summary>
    [MemoryPackable]
    public partial struct ConnectionData
    {
        /// <summary>
        /// The key of the connection
        /// </summary>
        public string Key { get; set; } = "BetterTogether";
        /// <summary>
        /// The initial states
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
        /// <summary>
        /// Sets the data associated with the specified key, or adds it if it doesn't exist
        /// </summary>
        /// <typeparam name="T">The type of the data. Must be MemoryPackable</typeparam>
        /// <param name="key">The key</param>
        /// <param name="data">The object</param>
        /// <returns>This object</returns>
        public readonly ConnectionData SetData<T>(string key, T data)
        {
            ExtraData[key] = MemoryPackSerializer.Serialize(data);
            return this;
        }
        /// <summary>
        /// Deletes the data associated with the specified key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>This object</returns>
        public readonly ConnectionData DeleteData(string key)
        {
            ExtraData.Remove(key);
            return this;
        }
    }
}
