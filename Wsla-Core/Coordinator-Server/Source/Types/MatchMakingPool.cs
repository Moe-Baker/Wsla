using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wsla.Server
{
    public class MatchMakingPool
    {
        public readonly MatchMakingApplication Application;
        public readonly MatchMakingPoolData Configuration;

        public string Name => Configuration.Name;
        public bool Backfill => Configuration.Backfill;
        public TimeSpan Duration { get; }

        List<MatchMakingTicket> Tickets;

        MatchMakingPoolDispatcher Dispatcher;

        public void Register(MatchMakingTicket ticket)
        {
            lock (Tickets)
            {
                Tickets.Add(ticket);
            }
        }
        public bool Unregister(MatchMakingTicket ticket)
        {
            lock (Tickets)
            {
                return Tickets.Remove(ticket);
            }
        }

        public void Refresh()
        {
            lock (Tickets)
            {
                if (Tickets.Count is 0)
                    return;

                var index = 0;

                CleanExpiredTickets(ref index);
                RunDispatcher(ref index);

                Tickets.RemoveAll(x => x is null);
            }
        }

        void CleanExpiredTickets(ref int index)
        {
            for (/* Start at Index */; index < Tickets.Count; index++)
            {
                if (Tickets[index].IsExpired() is false)
                    break;

                Tickets[index].Fail(WslaErrorCode.Timeout);
                Tickets[index] = null;
            }
        }

        void RunDispatcher(ref int index)
        {
            //Check if Number of Remaining Tickets can Fill a Room
            if ((Tickets.Count - index) < Configuration.Capacity.Min)
                return;

            Dispatcher.Clear();

            for (/* Start at Index */; index < Tickets.Count; index++)
            {
                var entry = MatchMakingPoolTicketEntry.For(Tickets, index);
                Dispatcher.TryJoin(entry);
            }

            foreach (var batch in Dispatcher.Batches)
            {
                //Enforce Balance
                batch.EnforceBalance();

                if (ValidateDispatch(batch) is false)
                {
                    Dispatcher.ReturnBatch(batch);
                    continue;
                }

                //Clear Out Tickets
                foreach (var entry in batch.Entries)
                    Tickets[entry.Index] = null;

                InvokeDispatch(batch).Forget();
            }
        }
        public bool ValidateDispatch(MatchMakingPoolBatch batch)
        {
            //Validate Min Count
            if (batch.Count < Configuration.Capacity.Min)
                return false;

            //Validate Full Considering Age
            if (batch.IsFull is false)
            {
                var age = batch.Age;
                var factor = Duration * 0.75f;

                if (age < factor)
                    return false;
            }

            //Validate Dispatch Rules
            if (MatchMakingRule.Validator.ValidateDispatch(batch) is false)
                return false;

            return true;
        }
        async Task InvokeDispatch(MatchMakingPoolBatch batch)
        {
            try
            {
                var parameters = batch.CalculateRoomParameters();

                var Regions = SparseArray.Clone(batch.Regions);

                var response = await CoordinatorServer.Matchmaking.CreateRoom(batch.GameVersion, Application.ID, Regions, parameters);
                if (response.IsError)
                {
                    batch.FailAll();
                    return;
                }

                var room = response.Value;

                room.SetPool(this);

                var info = room.GetConnectionInfo();

                batch.AcceptAll(info);
            }
            catch (Exception ex)
            {
                NetworkLog.Error($"Matchmaking Create Room Failed");
                NetworkLog.Error(ex);

                batch.FailAll();
                return;
            }
            finally
            {
                Dispatcher.ReturnBatch(batch);
            }
        }

        public MatchMakingPool(MatchMakingApplication Application, MatchMakingPoolData Configuration)
        {
            this.Application = Application;
            this.Configuration = Configuration;

            Duration = TimeSpan.FromSeconds(Configuration.Duration);

            Tickets = new();

            Dispatcher = new();
        }
    }

    public class MatchMakingPoolDispatcher
    {
        public List<MatchMakingPoolBatch> Batches { get; }

        public bool TryJoin(MatchMakingPoolTicketEntry entry)
        {
            //Iterate Existing Batches
            {
                foreach (var batch in Batches)
                    if (batch.TryJoin(entry))
                        return true;
            }

            //Create New Batch
            if (MatchMakingRule.Validator.ValidateCreate(entry.Ticket))
            {
                var batch = BatchPool.Take();
                batch.Assign(entry);
                Batches.Add(batch);
                return true;
            }

            return false;
        }
        public void Clear()
        {
            Batches.Clear();
        }

        public MatchMakingPoolDispatcher()
        {
            Batches = new List<MatchMakingPoolBatch>();

            BatchPool = new(() => new MatchMakingPoolBatch())
            {
                Reset = (x) => x.Clear()
            };
        }

        ObjectPool<MatchMakingPoolBatch> BatchPool;
        public void ReturnBatch(MatchMakingPoolBatch batch) => BatchPool.Return(batch);
    }

    public class MatchMakingPoolBatch
    {
        public MatchMakingPool Pool;
        public TimeSpan Age;

        public NetworkVersion GameVersion;

        public List<MatchMakingPoolTicketEntry> Entries { get; }
        public MatchMakingTicket this[int index] => Entries[index].Ticket;
        public byte Count => (byte)Entries.Count;
        public bool IsFull => Count >= Pool.Configuration.Capacity.Max;

        public List<ServerRegion> Regions { get; }

        public void Assign(MatchMakingPoolTicketEntry entry)
        {
            var ticket = entry.Ticket;

            GameVersion = ticket.GameVersion;
            Pool = ticket.Pool;
            Age = ticket.CalculateAge();

            Entries.Add(entry);

            foreach (var region in ticket.Regions)
                Regions.Add(region);
        }
        public void Clear()
        {
            Pool = default;

            Entries.Clear();
            Regions.Clear();
        }

        public bool TryJoin(MatchMakingPoolTicketEntry entry)
        {
            if (IsFull) return false;

            var ticket = entry.Ticket;

            if (ticket.GameVersion != GameVersion)
                return false;

            if (CheckAllowRegion(ticket.Regions) is false)
                return false;

            if (MatchMakingRule.Validator.ValidateJoin(this, ticket) is false)
                return false;

            Entries.Add(entry);
            CombineRegionList(ticket.Regions);
            return true;
        }

        bool CheckAllowRegion(SparseArray<ServerRegion> input)
        {
            foreach (var item in input)
                if (Regions.Contains(item))
                    return true;

            return false;
        }
        void CombineRegionList(SparseArray<ServerRegion> input)
        {
            Regions.RemoveAll(x => input.Contains(x) is false);
        }

        public void EnforceBalance()
        {
            if (Pool.Configuration.Balanced is false)
                return;

            if (Pool.Configuration.Backfill is true)
                return;

            if (Entries.Count % 2 == 0)
                return;

            Entries.RemoveAt(Entries.Count - 1);
        }

        public MatchMakingTicket GetOldestTicket() => Entries[0].Ticket;

        public CreateRoomParameters CalculateRoomParameters()
        {
            var Capacity = Pool.Backfill ? Pool.Configuration.Capacity.Max : Count;
            var Scenes = GetOldestTicket().Scenes;
            var Privacy = Pool.Backfill ? RoomPrivacy.Public : RoomPrivacy.Private;
            var Lock = Pool.Backfill ? RoomLockPolicy.None : RoomLockPolicy.AfterFill;
            var Shutdown = Pool.Configuration.ShutdownPolicy;

            return new CreateRoomParameters(Pool.Configuration.Name, Capacity, Scenes, Password: default, Privacy, Lock, Shutdown);
        }

        public void AcceptAll(RoomConnectionInfo info)
        {
            foreach (var entry in Entries)
                entry.Ticket.Accept(info);
        }

        public void FailAll() => FailAll(WslaErrorCode.InternalError);
        public void FailAll(WslaErrorCode error)
        {
            foreach (var entry in Entries)
                entry.Ticket.Fail(error);
        }

        public MatchMakingPoolBatch()
        {
            Entries = new();
            Regions = new();
        }
    }

    public record struct MatchMakingPoolTicketEntry(MatchMakingTicket Ticket, int Index)
    {
        public static MatchMakingPoolTicketEntry For(List<MatchMakingTicket> list, int index) => new MatchMakingPoolTicketEntry(list[index], index);
    }
}