using BetterTogetherCore.Models;
using MemoryPack;
using System.Collections.Generic;

namespace BetterTogetherCore.Extensions
{
    public static class ConnectionDataExtensions
    {
        /// <summary>
        /// Sets the data associated with the specified key, or adds it if it doesn't exist
        /// </summary>
        /// <typeparam name="T">The type of the data. Must be MemoryPackable</typeparam>
        /// <param name="key">The key</param>
        /// <param name="data">The object</param>
        /// <returns>This object</returns>
        public static ConnectionData SetData<T>(this ConnectionData cd, string key, T data)
        {
            cd.ExtraData[key] = MemoryPackSerializer.Serialize(data);
            return cd;
        }
        /// <summary>
        /// Deletes the data associated with the specified key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>This object</returns>
        public static ConnectionData DeleteData(this ConnectionData cd, string key)
        {
            cd.ExtraData.Remove(key);
            return cd;
        }
    }
}
