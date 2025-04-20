namespace Wsla
{
    public struct RelayServerRegistrationInfo
    {
        public string Address { get; set; }
        public ServerRegion Region { get; set; }

        public RelayServerRegistrationInfo(string Address, ServerRegion Region)
        {
            this.Address = Address;
            this.Region = Region;
        }
    }
}