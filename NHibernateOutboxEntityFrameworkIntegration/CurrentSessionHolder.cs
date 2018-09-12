using System;
using System.Threading;
using NServiceBus.Persistence;

class CurrentSessionHolder
{
    public SynchronizedStorageSession Current => pipelineContext.Value;

    public IDisposable SetCurrentPipelineContext(SynchronizedStorageSession context)
    {
        if (pipelineContext.Value != null)
        {
            throw new InvalidOperationException("Attempt to overwrite an existing pipeline context in BusSession.Current.");
        }

        pipelineContext.Value = context;
        return new ContextScope(this);
    }

    readonly AsyncLocal<SynchronizedStorageSession> pipelineContext = new AsyncLocal<SynchronizedStorageSession>();

    class ContextScope : IDisposable
    {
        public ContextScope(CurrentSessionHolder sessionHolder)
        {
            this.sessionHolder = sessionHolder;
        }

        public void Dispose()
        {
            sessionHolder.pipelineContext.Value = null;
        }

        readonly CurrentSessionHolder sessionHolder;
    }
}