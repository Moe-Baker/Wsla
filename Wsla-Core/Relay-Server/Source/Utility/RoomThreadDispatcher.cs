using LiteNetLib.Utils;

using System.Diagnostics;

namespace Wsla.Server
{
    public class RoomThreadDispatcher
    {
        Processor[] Processors;
        public class Processor
        {
            public readonly int ID;

            readonly Thread Thread;

            internal volatile int Allocations;

            readonly List<Room> Registrations;
            readonly List<Room> Collection;

            readonly TimeSpan TickDuration;

            readonly Stopwatch Stopwatch;

            public PoolsProperty Pools { get; }
            public struct PoolsProperty
            {
                public SingleInstancePool<NetDataWriter> SinglePackerWriter { get; init; }
                public GenericPool<NetDataWriter> MultiPackerWriter { get; init; }
                public SingleInstancePool<List<NetworkEntity>> EntityList { get; init; }

                public static PoolsProperty Create() => new PoolsProperty()
                {
                    SinglePackerWriter = new(new(true, 2048), x => x.SetPosition(0)),
                    MultiPackerWriter = new(() => new NetDataWriter(true, 128), x => x.SetPosition(0)),
                    EntityList = new(new(100), x => x.Clear()),
                };
            }

            /// <summary>
            /// Thread safe registration
            /// </summary>
            public void Register(Room item)
            {
                lock (Registrations)
                {
                    Registrations.Add(item);
                }

                Interlocked.Increment(ref Allocations);
            }
            /// <summary>
            /// Not thread safe un-registration 
            /// </summary>
            public void Unregister(Room item)
            {
                Remove(item);

                Interlocked.Decrement(ref Allocations);
            }

            void Add(Room item)
            {
                Collection.Add(item);
            }
            void Remove(Room item)
            {
                Collection.Remove(item);
            }

            void Tick()
            {
                Stopwatch.Start();

                while (true)
                {
                    var elapsed = Stopwatch.Elapsed;

                    Stopwatch.Restart();

                    //Add
                    lock (Registrations)
                    {
                        Collection.AddRange(Registrations);
                        Registrations.Clear();
                    }

                    //Receive
                    {
                        for (int i = Collection.Count - 1; i >= 0; i--)
                            Collection[i].Receive();
                    }

                    //Send
                    {
                        for (int i = Collection.Count - 1; i >= 0; i--)
                        {
                            var deltaTime = elapsed + Stopwatch.Elapsed;

                            //NetworkLog.Trace($"Delta Time: {(deltaTime}ms");

                            Collection[i].Send(deltaTime);
                        }
                    }

                    //Sleep
                    {
                        var remaining = TickDuration - Stopwatch.Elapsed;

                        //NetworkLog.Trace($"Thread Dispatcher Process Took {Stopwatch.Elapsed.TotalMilliseconds}ms to Complete");

                        if (remaining > TimeSpan.Zero)
                        {
                            //NetworkLog.Trace($"Sleep Remaining Duration : {remaining.TotalMilliseconds}ms");

                            Thread.Sleep(remaining);
                        }
                        else
                        {
                            NetworkLog.Warning($"Thread Dispatcher Process {ID} Overloaded, Took {Stopwatch.Elapsed.TotalMilliseconds}ms to Complete");
                        }
                    }
                }
            }

            public Processor(int ID, TimeSpan TickDuration)
            {
                this.ID = ID;
                this.TickDuration = TickDuration;

                Registrations = new List<Room>();
                Collection = new List<Room>();

                Stopwatch = new Stopwatch();

                Thread = new Thread(Tick);
                Thread.Start();

                Pools = PoolsProperty.Create();
            }
        }

        public Processor Retrieve()
        {
            var Marker = Processors[0];

            for (int i = 1; i < Processors.Length; i++)
            {
                var entry = Processors[i];

                if (entry.Allocations < Marker.Allocations)
                    Marker = entry;
            }

            return Marker;
        }

        public RoomThreadDispatcher(TimeSpan TickDuration) : this(Environment.ProcessorCount, TickDuration) { }
        public RoomThreadDispatcher(int Count, TimeSpan TickDuration)
        {
            Processors = new Processor[Count];

            for (int i = 0; i < Processors.Length; i++)
                Processors[i] = new Processor(i, TickDuration);
        }
    }
}