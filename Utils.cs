using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace BetterTogetherCore
{
    internal static class Utils
    {
        /// <summary>
        /// Fast implementation of StartsWith
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool FastStartsWith(this string str, string value)
        {
            if (str.Length < value.Length) return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (str[i] != value[i]) return false;
            }
            return true;
        }
        public static Regex guidRegex = new Regex(@"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}");
    }
    /// <summary>
    /// THis struct is sent to the server to establish a connection along with initial states
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
        public Dictionary<string, byte[]> InitStates { get; set; } = new Dictionary<string, byte[]>();

        /// <summary>
        /// Constructor 
        /// </summary>
        public ConnectionData() {}
        /// <summary>
        /// Constructor with key
        /// </summary>
        /// <param name="key">The key</param>
        public ConnectionData(string key)
        {
            Key = key;
        }
        /// <summary>
        /// Constructor with key and initial states
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="initStates">Initial states</param>
        [MemoryPackConstructor]
        public ConnectionData(string key, Dictionary<string, byte[]> initStates)
        {
            Key = key;
            InitStates = initStates;
        }
        /// <summary>
        /// Sets the specified state
        /// </summary>
        /// <typeparam name="T">The type of the state. Must be MemoryPackable</typeparam>
        /// <param name="key">The key of the state</param>
        /// <param name="data">The object</param>
        /// <returns>This object</returns>
        public readonly ConnectionData SetState<T>(string key, T data)
        {
            InitStates[key] = MemoryPackSerializer.Serialize(data);
            return this;
        }
        /// <summary>
        /// Deletes the specified state
        /// </summary>
        /// <param name="key">The key of the state</param>
        /// <returns>This object
        public readonly ConnectionData DeleteState(string key)
        {
            InitStates.Remove(key);
            return this;
        }
    }
}