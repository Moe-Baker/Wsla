namespace Wsla
{
    public struct RelayServerAdminInfo
    {
        public Guid ID;

        public RelayServerRegistrationInfo Info;

        public int Occupancy;

        public RelayServerAdminInfo(Guid ID, RelayServerRegistrationInfo Info, int Occupancy)
        {
            this.ID = ID;
            this.Info = Info;
            this.Occupancy = Occupancy;
        }
    }

    public struct RelayRoomAdminInfo
    {
        public RoomStateInfo Info;

        public RelayRoomAdminInfo(FixedString<FS20> Name, byte Capacity, byte Occupancy)
        {
            Info = new RoomStateInfo(Name, Capacity, Occupancy);
        }
        public RelayRoomAdminInfo(RoomStateInfo Info)
        {
            this.Info = Info;
        }
    }
}