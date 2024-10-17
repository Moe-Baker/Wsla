using System;
using System.Diagnostics;

using Debug = UnityEngine.Debug;

namespace Toolbox
{
    public class ExecutionTimer : IDisposable
    {
        string Name;
        Stopwatch Stopwatch;

        public ExecutionTimer() : this("Un-Named Timer") { }
        public ExecutionTimer(string name)
        {
            this.Name = name;
            Stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            Stopwatch.Stop();

            Debug.Log($"Execution for ({Name}) Took {Stopwatch.Elapsed.TotalMilliseconds.ToString("N2")}ms");
        }
    }
}