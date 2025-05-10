using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Wsla.Server
{
    public class RelayServer : IDisposable
    {
        public Guid ID { get; }

        public RelayServerInfo Info { get; }

        public MessagingPeer MessagingPeer { get; }

        public ServerRegion Region => Info.Region;
        public IPAddress Address => Info.Address;

        Dictionary<Guid, Room> Rooms;

        public int Occupancy;
        public void ModifyOccupancy(int modifier)
        {
            var value = Interlocked.Add(ref Occupancy, modifier);
            NetworkLog.Trace($"Relay {this} Occupancy Changed to {value}");
        }

        public RelayServerRegistration GetRegistration() => new RelayServerRegistration(ID, Info, Occupancy);

        public Room CreateRoom(ApplicationID application, Guid id, ushort port, CreateRoomParameters parameters, int reservations)
        {
            lock (Rooms)
            {
                if (Rooms.ContainsKey(id))
                    throw new Exception($"Room with ID {id} Already Registered");

                var room = new Room(this, application, port, parameters.Name, parameters.Capacity, 0, parameters.Privacy);
                Rooms.Add(id, room);

                room.MakeJoinReservation(reservations);

                return room;
            }
        }

        public bool RemoveRoom(Guid id)
        {
            lock (Rooms)
            {
                if (Rooms.Remove(id, out var room) is false)
                {
                    NetworkLog.Warning($"No Room with ID {id} Registered");
                    return false;
                }

                ModifyOccupancy(-room.Occupancy);
                return true;
            }
        }

        public void UpdateRooms(IEnumerable<UpdateRoomRequest> requests)
        {
            lock (Rooms)
            {
                foreach (var request in requests)
                {
                    if (Rooms.TryGetValue(request.ID, out var room) is false)
                    {
                        NetworkLog.Error($"No Room With ID {request.ID} Found to Update");
                        continue;
                    }

                    room.UpdateRoom(request.Parameters);
                }
            }
        }

        public void QueryRooms(ApplicationID application, List<RoomListEntryInfo> list)
        {
            lock (Rooms)
            {
                foreach (var (id, room) in Rooms)
                {
                    if (room.Privacy is RoomPrivacy.Private)
                        continue;

                    if (room.Application != application)
                        continue;

                    var connection = new RoomConnectionInfo(Address, room.Port);

                    var name = room.Name;
                    var capacity = room.Capacity;
                    var occupancy = room.Occupancy;

                    var entry = new RoomListEntryInfo(name, capacity, occupancy, connection);

                    list.Add(entry);
                }
            }
        }

        public void ListRooms(List<RelayRoomRegistration> list)
        {
            lock (Rooms)
            {
                list.EnsureCapacity(Rooms.Count);

                foreach (var (id, room) in Rooms)
                {
                    var registration = room.GetRegistration();
                    list.Add(registration);
                }
            }
        }

        public bool TryReserveRoom(in RoomQueryFilter filter, out Room target)
        {
            lock (Rooms)
            {
                foreach (var (id, room) in Rooms)
                {
                    if (room.Application != filter.Application)
                        continue;

                    if (room.Pool != filter.Pool)
                        continue;

                    if (room.Privacy is RoomPrivacy.Private)
                        continue;

                    if (room.CheckVacancy() < filter.Vacancy)
                        continue;

                    room.MakeJoinReservation(filter.Vacancy);

                    target = room;
                    return true;
                }
            }

            target = default;
            return false;
        }

        public void Dispose() { }

        public override string ToString() => Info.ToString();

        public RelayServer(RelayServerInfo Info, MessagingPeer MessagingPeer)
        {
            ID = Guid.NewGuid();

            this.Info = Info;
            this.MessagingPeer = MessagingPeer;

            Rooms = new();
        }

        public static RelayServer Create(RegisterRelayRequest request, MessagingPeer peer)
        {
            var server = new RelayServer(request.Info, peer);

            if (request.Rooms?.Count > 0)
            {
                server.Rooms.EnsureCapacity(request.Rooms.Count);

                var occupancy = 0;

                foreach (var entry in request.Rooms)
                {
                    var room = Room.Create(server, entry);
                    server.Rooms.Add(entry.ID, room);

                    occupancy += room.Occupancy;
                }

                server.ModifyOccupancy(occupancy);
            }

            return server;
        }
    }
}