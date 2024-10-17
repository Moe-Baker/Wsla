using System;
using System.Threading;

using Cysharp.Threading.Tasks;

namespace Toolbox
{
    /// <summary>
    /// A type that handles task and optimizes task cancellation
    /// </summary>
    public class UniProcedure
    {
        UniTask Task;
        CancellationTokenSource CancellationSource;

        public Handle Reserve() => new Handle(this);
        public ref struct Handle
        {
            UniProcedure Procedure;

            public CancellationToken CancellationToken { get; }

            bool Assigned;

            public UniTask Await(UniTask value)
            {
                if (Assigned)
                    throw new InvalidOperationException($"Procedure Task Already Assigned");

                Assigned = true;

                return Procedure.Await(value);
            }

            public void Dispose()
            {
                if (Assigned == false)
                    throw new InvalidOperationException($"Procedure Task not Assigned Before Dispose");
            }

            public Handle(UniProcedure procedure)
            {
                this.Procedure = procedure;
                CancellationToken = Procedure.Reset();
                Assigned = false;
            }
        }

        public UniTask Await(UniTask target)
        {
            var awaiter = target.GetAwaiter();

            if (awaiter.IsCompleted)
                return ShortAwait(target);
            else
                return LongAwait(target);
        }
        UniTask ShortAwait(UniTask target)
        {
            return UniTask.CompletedTask;
        }
        async UniTask LongAwait(UniTask target)
        {
            Task = target;

            await target;

            Task = UniTask.CompletedTask;
        }

        void ClearTask()
        {
            Task = UniTask.CompletedTask;
        }

        public CancellationToken Reset()
        {
            if (Task.Status is UniTaskStatus.Pending)
            {
                CancellationSource?.Cancel();
                CancellationSource = new CancellationTokenSource();
            }

            return CancellationSource.Token;
        }

        public UniProcedure()
        {
            Task = UniTask.CompletedTask;
            CancellationSource = new CancellationTokenSource();
        }
    }
}