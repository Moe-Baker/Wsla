using System.Diagnostics.CodeAnalysis;
using System;
using Wsla.Serialization;

namespace Wsla
{
    public enum RemoteBufferMode : byte
    {
        None, Buffer
    }

    public interface IRemoteSyncMemberID
    {
        byte Value { get; }
    }
}