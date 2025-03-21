using System;

public struct AtomicTime
{
    DateTime Timestamp;

    object Lock;

    public void UpdateTime() => WriteTime(UTC);
    public void WriteTime(DateTime time)
    {
        lock (Lock)
        {
            Timestamp = time;
        }
    }

    public DateTime ReadTime()
    {
        lock (Lock)
        {
            return Timestamp;
        }
    }

    public TimeSpan ReadSpan()
    {
        var time = ReadTime();

        return (UTC - time).Duration();
    }

    AtomicTime(DateTime Timestamp, object Lock)
    {
        this.Timestamp = Timestamp;
        this.Lock = Lock;
    }

    public static AtomicTime Create() => new AtomicTime(UTC, new object());

    public static DateTime UTC => DateTime.UtcNow;
}