using MemoryPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace BetterTogetherCore.State
{
    public class StateManager
    {
        private ConcurrentDictionary<string, byte[]> _States = new ConcurrentDictionary<string, byte[]>();
        /// <summary>
        /// Creates a copy of the states dictionary.
        /// </summary>
        public Dictionary<string, byte[]> States { get { return new Dictionary<string, byte[]>(_States); } }
        public void SetState(string key, byte[] value)
        {
            _States[key] = value;
        }
        public void SetState<T>(string key, T value)
        {
            byte[] data = MemoryPackSerializer.Serialize(value);
            if(data.Length > 0 ) _States[key] = data;
        }
        public byte[] GetState(string key)
        {
            if (_States.ContainsKey(key))
            {
                return _States[key];
            }
            return [];
        }
        public T? GetState<T>(string key)
        {
            if (_States.ContainsKey(key))
            {
                return MemoryPackSerializer.Deserialize<T>(_States[key]);
            }
            return default;
        }

        public void Clear()
        {
            _States.Clear();
        }
        public void ClearExcept(List<string> except)
        {
            foreach (var key in _States.Keys)
            {
                if (!except.Contains(key))
                {
                    _States.TryRemove(key, out _);
                }
            }
        }
        public void ClearIncluding(List<string> including)
        {
            foreach (var key in _States.Keys)
            {
                if (including.Contains(key))
                {
                    _States.TryRemove(key, out _);
                }
            }
        }

        public byte[] this[string key]
        {
            get { return GetState(key); }
            set { SetState(key, value); }
        }
        public bool ContainsKey(string key)
        {
            return _States.ContainsKey(key);
        }
        public void Remove(string key)
        {
            _States.TryRemove(key, out _);
        }
    }
}
