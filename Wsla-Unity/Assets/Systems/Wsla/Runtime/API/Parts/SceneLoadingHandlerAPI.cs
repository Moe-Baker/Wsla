using System;
using System.Collections.Generic;

namespace Wsla.Unity
{
    partial class NetworkAPI
    {
        internal SceneLoadingController SceneLoadController;

        public void RegisterSceneLoadHandler<T>()
            where T : ISceneLoadingHandler, new()
        {
            var instance = new T();
            RegisterSceneLoadHandler(instance);
        }
        public void RegisterSceneLoadHandler(ISceneLoadingHandler value)
        {
            SceneLoadController = new SceneLoadingController(value);
        }
    }

    class SceneLoadingController
    {
        ISceneLoadingHandler Contract;
        public void StartLoading(int scenes)
        {
            Reserve(scenes);
            Contract.StartLoading();
        }
        public void EndLoading() => Contract.EndLoading();

        List<Entry> Entries;
        public class Entry : IProgress<float>
        {
            readonly SceneLoadingController Handler;

            public float Value { get; private set; }
            public void Report(float value)
            {
                Value = value;

                Handler.Report(this);
            }
            public void Clear()
            {
                Value = 0;
            }

            public Entry(SceneLoadingController Handler)
            {
                this.Handler = Handler;
            }
        }

        void Report(Entry entry)
        {
            var value = Calculate();
            Contract.ReportProgress(value);
        }
        float Calculate()
        {
            var value = 0f;

            for (int i = 0; i < Steps; i++)
                value += Entries[i].Value;

            return value / Steps;
        }

        int Steps;
        void Reserve(int Steps)
        {
            this.Steps = Steps;
            this.Index = 0;

            if (Entries.Capacity < Steps)
                Entries.Capacity = Steps;

            while (Entries.Count < Steps)
            {
                var entry = new Entry(this);
                Entries.Add(entry);
            }

            for (int i = 0; i < Steps; i++)
                Entries[i].Clear();
        }

        int Index;
        public IProgress<float> RetrieveSurrogate()
        {
            var entry = Entries[Index];
            Index += 1;
            return entry;
        }

        public SceneLoadingController(ISceneLoadingHandler Contract)
        {
            this.Contract = Contract;

            Entries = new(1);
        }
    }
    public interface ISceneLoadingHandler
    {
        void StartLoading();
        void ReportProgress(float progress);
        void EndLoading();
    }
}