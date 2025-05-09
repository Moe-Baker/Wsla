using System;

namespace Wsla.Server
{
    public class Room
    {
        public RelayServer Server { get; }

        public ApplicationID Application { get; }

        public ushort Port { get; }

        public FixedString<FS20> Name { get; }

        public byte Capacity;

        public byte Occupancy;
        public bool IsFull => Occupancy >= Capacity;

        public RoomPrivacy Privacy;

        public bool IsLocked;
        void Lock()
        {
            IsLocked = true;
            Privacy = RoomPrivacy.Private;
        }

        TimedReservationCollection JoinReservations;
        public void MakeJoinReservation(int capacity) => JoinReservations.ReserveCapacity(capacity);

        public int CheckVacancy()
        {
            var total = Occupancy + JoinReservations.CalculateCapacity();

            var vacancy = Capacity - total;
            if (vacancy < 0) vacancy = 0;

            return vacancy;
        }

        public RelayRoomRegistration GetRegistration() => new RelayRoomRegistration(Name, Capacity, Occupancy);

        public MatchMakingPool Pool { get; private set; }
        public void SetPool(MatchMakingPool value)
        {
            Pool = value;
        }

        public RoomConnectionInfo GetConnectionInfo() => new RoomConnectionInfo(Server.Address, Port);

        public void UpdateRoom(UpdateRoomParameters parameters)
        {
            //Lock
            if (parameters.Lock)
            {
                Lock();
            }

            //Free Reservations
            if (parameters.Joins > 0)
            {
                JoinReservations.FreeCapacity(parameters.Joins);
            }

            //Update Occupancy
            if (parameters.Occupancy.HasValue)
            {
                var delta = (parameters.Occupancy.Value - Occupancy);
                Server.ModifyOccupancy(delta);

                Occupancy = parameters.Occupancy.Value;
            }
        }

        public Room(RelayServer Server, ApplicationID Application, ushort Port, FixedString<FS20> Name, byte Capacity, byte Occupancy, RoomPrivacy Privacy)
        {
            this.Application = Application;
            this.Server = Server;
            this.Port = Port;
            this.Name = Name;
            this.Capacity = Capacity;
            this.Occupancy = Occupancy;
            this.Privacy = Privacy;

            IsLocked = false;

            JoinReservations = new TimedReservationCollection(TimeSpan.FromSeconds(10));
        }

        public static Room Create(RelayServer server, RoomMatchmakerEntryData data)
        {
            var state = data.State;

            return new Room(server, data.Application, data.Port, state.Name, state.Capacity, state.Occupancy, data.Privacy);
        }
    }

    public ref struct RoomQueryFilter
    {
        public ApplicationID Application { get; }

        public Span<ServerRegion> Regions { get; }
        public bool CheckRegion(ServerRegion target)
        {
            for (int i = 0; i < Regions.Length; i++)
            {
                if (Regions[i] == target)
                    return true;
            }

            return false;
        }

        public int Vacancy { get; }

        public MatchMakingPool Pool { get; }

        public RoomQueryFilter(ApplicationID Application, Span<ServerRegion> Regions, int Vacancy)
        {
            this.Application = Application;
            this.Regions = Regions;
            this.Vacancy = Vacancy;

            Pool = default;
        }
        public RoomQueryFilter(MatchMakingPool Pool, Span<ServerRegion> Regions, int Vacancy)
        {
            Application = Pool.Application.ID;

            this.Pool = Pool;
            this.Regions = Regions;
            this.Vacancy = Vacancy;
        }
    }
}