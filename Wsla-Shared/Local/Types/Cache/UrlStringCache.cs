using System;
using System.Collections.Generic;
using System.Net;

namespace Wsla
{
    public class UrlStringCache
    {
        Dictionary<Entry, string> Dictionary { get; }
        struct Entry : IEquatable<Entry>
        {
            public IPAddress Address { get; }
            public ushort Port { get; }
            public string Path { get; }

            public override bool Equals(object obj)
            {
                if (obj is Entry other)
                    return Equals(other);

                return false;
            }
            public bool Equals(Entry other)
            {
                if (Port != other.Port)
                    return false;

                if (Path != other.Path)
                    return false;

                if (Address.Equals(other.Address))
                    return false;

                return true;
            }

            public override int GetHashCode() => HashCode.Combine(Address, Port, Path);

            public Entry(IPAddress Address, ushort Port, string Path)
            {
                this.Address = Address;
                this.Port = Port;
                this.Path = Path;
            }
        }

        public string Get(IPAddress address, ushort port, string path)
        {
            var entry = new Entry(address, port, path);

            if (Dictionary.TryGetValue(entry, out var url) is false)
            {
                url = $"http://{address}:{port}/{path}";
                Dictionary[entry] = url;
            }

            return url;
        }

        public UrlStringCache()
        {
            Dictionary = new Dictionary<Entry, string>();
        }
    }
}