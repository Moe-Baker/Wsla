using System;

namespace Wsla
{
    public static class NetworkLog
    {
        public static HandlerDelegate Handler;
        public delegate void HandlerDelegate(NetworkLogType type, object item);

        static object ThreadLock = new object();

        public static void UseConsole()
        {
            Handler = (type, item) =>
            {
                lock (ThreadLock)
                {
                    Console.ForegroundColor = type switch
                    {
                        NetworkLogType.Trace => ConsoleColor.White,
                        NetworkLogType.Info => ConsoleColor.DarkGreen,
                        NetworkLogType.Warning => ConsoleColor.DarkYellow,
                        NetworkLogType.Error => ConsoleColor.DarkRed,
                        _ => throw new NotImplementedException(),
                    };

                    Console.WriteLine($"[{type}] [{DateTime.Now}]: {item}");

                    Console.ResetColor();
                }
            };
        }

        public static void Submit(NetworkLogType type, object item)
        {
            Handler(type, item);
        }

        public static void Trace(object item) => Submit(NetworkLogType.Trace, item);
        public static void Info(object item) => Submit(NetworkLogType.Info, item);
        public static void Warning(object item) => Submit(NetworkLogType.Warning, item);
        public static void Error(object item) => Submit(NetworkLogType.Error, item);
    }

    public enum NetworkLogType
    {
        Trace,
        Info,
        Warning,
        Error
    }
}