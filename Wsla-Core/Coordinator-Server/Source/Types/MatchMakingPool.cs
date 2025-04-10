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
        public TimeSpan Duration { get; }
        public bool Backfill => Configuration.Backfill;

        List<MatchMakingTicket> List;

        public void Register(MatchMakingTicket ticket)
        {
            lock (List)
            {
                List.Add(ticket);
            }
        }
        public bool Unregister(MatchMakingTicket ticket)
        {
            lock (List)
            {
                return List.Remove(ticket);
            }
        }

        public void Refresh()
        {
            lock (List)
            {
                var index = 0;

                if (List.Count is 0)
                    return;

                //Skip Expired Tickets
                for (/* Start at Index */; index < List.Count; index++)
                {
                    if (List[index].IsExpired() is false)
                        break;

                    List[index].Fail(WslaErrorCode.Timeout);
                    List[index] = null;
                }

                var allocations = (List.Count - index); //Count of Remaining Valid Tickets

                //Dispatch Remaining Tickets
                if (allocations >= Configuration.Capacity.Min)
                {
                    var dispatcher = new MatchMakingPoolDispatcher(this);

                    for (/* Start at Index */; index < List.Count; index++)
                    {
                        var entry = MatchMakingPoolTicketEntry.For(List, index);
                        dispatcher.Accept(entry);
                    }

                    foreach (var batch in dispatcher.Batches)
                    {
                        batch.EnforceBalance();

                        if (IsValid(batch) is false)
                            continue;

                        foreach (var entry in batch.Entries)
                            List[entry.Index] = null;

                        Dispatch(batch).Forget();
                    }
                }

                List.RemoveAll(x => x is null);
            }
        }

        bool IsValid(MatchMakingPoolBatch batch)
        {
            if (batch.Count < Configuration.Capacity.Min)
                return false;

            //Validate Age
            if (batch.IsFull is false)
            {
                var ticket = batch.GetOldestTicket();

                var age = ticket.CalculateAge();
                var factor = Duration * 0.75f;

                if (age < factor)
                    return false;
            }

            return true;
        }

        public bool ValidateRules(MatchMakingPoolBatch batch, MatchMakingTicket ticket)
        {
            if (Configuration.Rules is null)
                return true;

            foreach (var rule in Configuration.Rules)
                if (rule.Validate(batch, ticket) is false)
                    return false;

            return true;
        }

        async Task Dispatch(MatchMakingPoolBatch batch)
        {
            var Capacity = Backfill ? Configuration.Capacity.Max : batch.Count;
            var Scene = batch.GetScene();
            var Privacy = Backfill ? RoomPrivacy.Public : RoomPrivacy.Private;
            var Lock = Backfill ? RoomLockPolicy.None : RoomLockPolicy.AfterFill;
            var Parameters = new CreateRoomParameters(Configuration.Name, Capacity, Scene, Password: default, Privacy, Lock);

            var Regions = SparseArray.Clone(batch.Regions);

            RoomConnectionInfo Info;

            try
            {
                var room = await CoordinatorServer.Matchmaking.CreateRoom(Application.ID, Regions, Parameters);

                room.SetPool(this);

                Info = room.GetConnectionInfo();
            }
            catch (Exception ex)
            {
                NetworkLog.Error($"Matchmaking Create Room Failed");
                NetworkLog.Error(ex);

                foreach (var entry in batch.Entries)
                    entry.Ticket.Fail(WslaErrorCode.InternalError);

                return;
            }

            foreach (var entry in batch.Entries)
                entry.Ticket.Accept(Info);
        }

        public MatchMakingPool(MatchMakingApplication Application, MatchMakingPoolData Configuration)
        {
            this.Application = Application;
            this.Configuration = Configuration;

            Duration = TimeSpan.FromSeconds(Configuration.Duration);

            List = new();
        }
    }

    public class MatchMakingPoolDispatcher
    {
        readonly MatchMakingPool Pool;

        public List<MatchMakingPoolBatch> Batches { get; }

        public MatchMakingPoolBatch Accept(MatchMakingPoolTicketEntry entry)
        {
            //Iterate Existing Batches
            {
                foreach (var batch in Batches)
                    if (batch.TryAccept(entry))
                        return batch;
            }

            //Create New Batch
            {
                var batch = new MatchMakingPoolBatch(Pool, entry);
                Batches.Add(batch);
                return batch;
            }
        }

        public MatchMakingPoolDispatcher(MatchMakingPool Pool)
        {
            this.Pool = Pool;

            Batches = new List<MatchMakingPoolBatch>();
        }
    }
    public class MatchMakingPoolBatch
    {
        readonly MatchMakingPool Pool;

        public List<MatchMakingPoolTicketEntry> Entries { get; }

        public byte Count => (byte)Entries.Count;

        public bool IsFull => Count >= Pool.Configuration.Capacity.Max;

        public List<ServerRegion> Regions { get; }

        public bool TryAccept(MatchMakingPoolTicketEntry entry)
        {
            if (IsFull) return false;

            var ticket = entry.Ticket;

            if (CheckAllowRegion(ticket.Regions) is false)
                return false;

            if (Pool.ValidateRules(this, ticket) is false)
                return false;

            Entries.Add(entry);
            CombineRegionList(ticket.Regions);
            return true;
        }

        public void EnforceBalance()
        {
            if (Pool.Configuration.Balanced is false)
                return;

            if (Pool.Configuration.Backfill is true)
                return;

            var imbalance = Entries.Count % 2;

            Entries.RemoveRange(Entries.Count - imbalance, imbalance);
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

        public MatchMakingTicket GetOldestTicket() => Entries[0].Ticket;

        public NetworkSceneID GetScene() => GetOldestTicket().Scene;

        public MatchMakingPoolBatch(MatchMakingPool Pool, MatchMakingPoolTicketEntry entry)
        {
            this.Pool = Pool;

            Entries = new() { entry };

            Regions = entry.Ticket.Regions.ToList();
        }
    }
    public record struct MatchMakingPoolTicketEntry(MatchMakingTicket Ticket, int Index)
    {
        public static MatchMakingPoolTicketEntry For(List<MatchMakingTicket> list, int index) => new MatchMakingPoolTicketEntry(list[index], index);
    }
}