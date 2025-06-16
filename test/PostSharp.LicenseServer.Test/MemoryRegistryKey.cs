using PostSharp.Platform;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SharpCrafters.LicenseServer.Test
{
    class MemoryRegistryKey : IRegistryKey
    {
        private Dictionary<string, object> values = new Dictionary<string, object>();
        private Dictionary<string, IRegistryKey> subKeys = new Dictionary<string, IRegistryKey>();

        public void Dispose()
        {
        }

        public string[] GetSubKeyNames()
        {
            List<string> subKeyNames = new List<string>(subKeys.Keys);
            return subKeyNames.ToArray();
        }

        public IRegistryKey OpenSubKey(string subKey)
        {
            if (subKeys.TryGetValue(subKey, out IRegistryKey subKeyValue))
            {
                return subKeyValue;
            }
            else
            {
                throw new ArgumentException($"Subkey '{subKey}' does not exist.");
            }
        }

        public IRegistryKey OpenSubKey(string name, bool writable)
        {
            if (subKeys.TryGetValue(name, out IRegistryKey subKeyValue))
            {
                return subKeyValue;
            }
            else
            {
                throw new ArgumentException($"Subkey '{name}' does not exist.");
            }
        }

        public IRegistryKey CreateSubKey(string subKey)
        {
            if (!subKeys.ContainsKey(subKey))
            {
                MemoryRegistryKey newSubKey = new MemoryRegistryKey();
                subKeys[subKey] = newSubKey;
                return newSubKey;
            }
            else
            {
                throw new ArgumentException($"Subkey '{subKey}' already exists.");
            }
        }

        public void DeleteSubKey(string subKey)
        {
            if (subKeys.ContainsKey(subKey))
            {
                subKeys.Remove(subKey);
            }
            else
            {
                throw new ArgumentException($"Subkey '{subKey}' does not exist.");
            }
        }

        public void DeleteSubKey(string subKey, bool throwOnMissingSubKey)
        {
            if (subKeys.ContainsKey(subKey))
            {
                subKeys.Remove(subKey);
            }
            else if (throwOnMissingSubKey)
            {
                throw new ArgumentException($"Subkey '{subKey}' does not exist.");
            }
        }

        public void DeleteSubKeyTree(string subKey)
        {
            if (subKeys.ContainsKey(subKey))
            {
                subKeys.Remove(subKey);
            }
            else
            {
                throw new ArgumentException($"Subkey '{subKey}' does not exist.");
            }
        }

        public void DeleteSubKeyTree(string subKey, bool throwOnMissingSubKey)
        {
            if (subKeys.ContainsKey(subKey))
            {
                subKeys.Remove(subKey);
            }
            else if (throwOnMissingSubKey)
            {
                throw new ArgumentException($"Subkey '{subKey}' does not exist.");
            }
        }

        public string[] GetValueNames()
        {
            List<string> valueNames = new List<string>(values.Keys);
            return valueNames.ToArray();
        }

        public object GetValue(string name)
        {
            if (values.TryGetValue(name, out object value))
            {
                return value;
            }
            else
            {
                throw new ArgumentException($"Value '{name}' does not exist.");
            }
        }

        public object GetValue(string name, object defaultValue)
        {
            if (values.TryGetValue(name, out object value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }

        public void SetValue(string name, string value)
        {
            if (values.ContainsKey(name))
            {
                values[name] = value;
            }
            else
            {
                values.Add(name, value);
            }
        }

        public void SetDWordValue(string name, int value)
        {
            if (values.ContainsKey(name))
            {
                values[name] = value;
            }
            else
            {
                values.Add(name, value);
            }
        }

        public void SetQWordValue(string name, long value)
        {
            if (values.ContainsKey(name))
            {
                values[name] = value;
            }
            else
            {
                values.Add(name, value);
            }
        }

        public void DeleteValue(string name)
        {
            if (values.ContainsKey(name))
            {
                values.Remove(name);
            }
            else
            {
                throw new ArgumentException($"Value '{name}' does not exist.");
            }
        }

        public void DeleteValue(string name, bool throwOnMissingSubKey)
        {
            if (values.ContainsKey(name))
            {
                values.Remove(name);
            }
            else if (throwOnMissingSubKey)
            {
                throw new ArgumentException($"Value '{name}' does not exist.");
            }
        }

        public void Close()
        {
            // No action needed for in-memory registry key.
        }

        public bool CanMonitorChanges => false;

        public SafeHandle Handle => null;
    }
}