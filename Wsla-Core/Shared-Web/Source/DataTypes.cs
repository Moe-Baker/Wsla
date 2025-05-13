namespace Wsla
{
    public struct RelayServerAdminInfo
    {
        public Guid ID;

        public RelayServerRegistrationInfo Registeration;

        public int Occupancy;

        public RelayServerAdminInfo(Guid ID, RelayServerRegistrationInfo Info, int Occupancy)
        {
            this.ID = ID;
            this.Registeration = Info;
            this.Occupancy = Occupancy;
        }
    }

    public struct RelayRoomAdminInfo
    {
        public RoomStateInfo State;

        public RelayRoomAdminInfo(FixedString<FS20> Name, byte Capacity, byte Occupancy)
        {
            State = new RoomStateInfo(Name, Capacity, Occupancy);
        }
        public RelayRoomAdminInfo(RoomStateInfo Info)
        {
            this.State = Info;
        }
    }
}