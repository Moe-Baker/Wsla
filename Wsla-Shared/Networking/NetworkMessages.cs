using MemoryPack;

namespace Wsla.Shared
{
    [MemoryPackable]
    public partial struct ClientConnectionRequest
    {
        public string ClientID { get; }
        public string Username { get; }

        public override string ToString() => $"Client ID: {ClientID}, Username: {Username}";

        public ClientConnectionRequest(string clientID, string username)
        {
            this.ClientID = clientID;
            this.Username = username;
        }
    }
}