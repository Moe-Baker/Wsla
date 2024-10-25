using LiteNetLib.Utils;

using System.Collections.Concurrent;
using System.Diagnostics;

using Wsla.Server;

namespace Wsla
{
    public class RoomThreadDispatcher
    {
        Processor[] Processors;
        public class Processor
        {
            public readonly int ID;

            readonly Thread Thread;

            internal volatile int Allocations;

            readonly ConcurrentBag<Room> Registerations;

            readonly TimeSpan TickDuration;

            Room? First;
            Room? Last;

            Stopwatch Stopwatch;

            internal readonly GenericPool<NetDataWriter> PacketWritersPool;

            /// <summary>
            /// Thread safe registeration
            /// </summary>
            public void Register(Room item)
            {
                Registerations.Add(item);

                Interlocked.Increment(ref Allocations);
            }

            /// <summary>
            /// Not thread safe unregisteration 
            /// </summary>
            public void Unregister(Room item)
            {
                Remove(item);

                Interlocked.Decrement(ref Allocations);
            }

            void Tick()
            {
                Stopwatch.Start();

                while (true)
                {
                    var elapsed = Stopwatch.Elapsed;

                    Stopwatch.Restart();

                    //Add
                    {
                        while (Registerations.TryTake(out var item))
                            Add(item);
                    }

                    //Receive
                    {
                        var pointer = First;

                        while (pointer is not null)
                        {
                            pointer.Receive();
                            pointer = pointer.Next;
                        }
                    }

                    //Send
                    {
                        var pointer = First;

                        while (pointer is not null)
                        {
                            var deltaTime = elapsed + Stopwatch.Elapsed;

                            //NetworkLog.Trace($"Delta Time: {(deltaTime}ms");

                            pointer.Send(deltaTime);
                            pointer = pointer.Next;
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

            void Add(Room item)
            {
                if (First is null)
                {
                    First = item;
                    Last = item;
                }
                else
                {
                    Last.Next = item;
                    Last = item;
                }
            }
            void Remove(Room item)
            {
                if (item == First)
                {
                    if (First == Last)
                    {
                        First = null;
                        Last = null;
                    }
                    else
                    {
                        First = item.Next;
                        First.Previous = default;
                    }
                }
                else if (item == Last)
                {
                    Last = item.Previous;
                    Last.Next = null;
                }
                else
                {
                    var previous = item.Previous;
                    var next = item.Next;

                    previous.Next = next;
                    next.Previous = previous;
                }

                item.Next = null;
                item.Previous = null;
            }

            public Processor(int ID, TimeSpan TickDuration)
            {
                this.ID = ID;
                this.TickDuration = TickDuration;

                PacketWritersPool = new GenericPool<NetDataWriter>(() => new NetDataWriter(true, 128));

                Registerations = new ConcurrentBag<Room>();

                Stopwatch = new Stopwatch();

                Thread = new Thread(Tick);
                Thread.Start();
            }
        }

        public Processor Retrieve()
        {
            (int Index, int Allocations) Marker = (0, int.MaxValue);

            for (int i = 0; i < Processors.Length; i++)
            {
                var entry = Processors[i];

                if (entry.Allocations < Marker.Allocations)
                    Marker = (i, entry.Allocations);
            }

            return Processors[Marker.Index];
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