using System.Collections.Concurrent;
using System.Diagnostics;

namespace Wsla
{
    public class ThreadDispatcher
    {
        Processor[] Processors;
        public class Processor
        {
            public readonly int ID;

            readonly Thread Thread;

            internal volatile int Allocations;

            readonly ConcurrentBag<IJob> Registerations;

            readonly TimeSpan TickDuration;

            IJob? First;
            IJob? Last;

            Stopwatch Stopwatch;

            /// <summary>
            /// Thread safe registeration
            /// </summary>
            public void Register(IJob item)
            {
                Registerations.Add(item);

                Interlocked.Increment(ref Allocations);
            }

            /// <summary>
            /// Not thread safe unregisteration 
            /// </summary>
            public void Unregister(IJob item)
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

            void Add(IJob item)
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
            void Remove(IJob item)
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

                Registerations = new ConcurrentBag<IJob>();

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

        public ThreadDispatcher(TimeSpan TickDuration) : this(Environment.ProcessorCount, TickDuration) { }
        public ThreadDispatcher(int Count, TimeSpan TickDuration)
        {
            Processors = new Processor[Count];

            for (int i = 0; i < Processors.Length; i++)
                Processors[i] = new Processor(i, TickDuration);
        }

        public interface IJob
        {
            IJob? Next { get; set; }
            IJob? Previous { get; set; }

            void Receive();

            void Send(TimeSpan elapsed);
        }
    }
}