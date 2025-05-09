namespace Wsla
{
    public struct RelayServerRegistration
    {
        public RelayServerInfo Info;

        public int Occupancy;

        public RelayServerRegistration(RelayServerInfo Info, int Occupancy)
        {
            this.Info = Info;
            this.Occupancy = Occupancy;
        }
    }

    public struct RelayRoomRegistration
    {
        public RoomStateInfo Info;

        public RelayRoomRegistration(FixedString<FS20> Name, byte Capacity, byte Occupancy)
        {
            Info = new RoomStateInfo(Name, Capacity, Occupancy);
        }
        public RelayRoomRegistration(RoomStateInfo Info)
        {
            this.Info = Info;
        }
    }
}