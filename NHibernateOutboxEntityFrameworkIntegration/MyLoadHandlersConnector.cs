using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Outbox;
using NServiceBus.Persistence;
using NServiceBus.Pipeline;
using NServiceBus.Transport;
using NServiceBus.Unicast;

class MyLoadHandlersConnector : StageConnector<IIncomingLogicalMessageContext, IInvokeHandlerContext>
{
    public MyLoadHandlersConnector(
        CurrentSessionHolder sessionHolder,
        MessageHandlerRegistry messageHandlerRegistry,
        ISynchronizedStorage synchronizedStorage,
        ISynchronizedStorageAdapter adapter)
    {
        this.sessionHolder = sessionHolder;
        this.messageHandlerRegistry = messageHandlerRegistry;
        this.synchronizedStorage = synchronizedStorage;
        this.adapter = adapter;
    }

    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<IInvokeHandlerContext, Task> stage)
    {
        var outboxTransaction = context.Extensions.Get<OutboxTransaction>();
        var transportTransaction = context.Extensions.Get<TransportTransaction>();
        using (var storageSession = await AdaptOrOpenNewSynchronizedStorageSession(transportTransaction, outboxTransaction, context.Extensions).ConfigureAwait(false))
        {
            //This line is the only difference from regular connector. It makes the storage session available via context by assigning it to an async local
            using (sessionHolder.SetCurrentPipelineContext(storageSession))
            {
                var handlersToInvoke = messageHandlerRegistry.GetHandlersFor(context.Message.MessageType);

                if (!context.MessageHandled && handlersToInvoke.Count == 0)
                {
                    var error = $"No handlers could be found for message type: {context.Message.MessageType}";
                    throw new InvalidOperationException(error);
                }

                foreach (var messageHandler in handlersToInvoke)
                {
                    messageHandler.Instance = context.Builder.Build(messageHandler.HandlerType);

                    var handlingContext = this.CreateInvokeHandlerContext(messageHandler, storageSession, context);
                    await stage(handlingContext).ConfigureAwait(false);

                    if (handlingContext.HandlerInvocationAborted)
                    {
                        //if the chain was aborted skip the other handlers
                        break;
                    }
                }

                context.MessageHandled = true;
                await storageSession.CompleteAsync().ConfigureAwait(false);
            }
        }
    }

    async Task<CompletableSynchronizedStorageSession> AdaptOrOpenNewSynchronizedStorageSession(TransportTransaction transportTransaction, OutboxTransaction outboxTransaction, ContextBag contextBag)
    {
        return await adapter.TryAdapt(outboxTransaction, contextBag).ConfigureAwait(false)
               ?? await adapter.TryAdapt(transportTransaction, contextBag).ConfigureAwait(false)
               ?? await synchronizedStorage.OpenSession(contextBag).ConfigureAwait(false);
    }

    readonly ISynchronizedStorageAdapter adapter;
    readonly ISynchronizedStorage synchronizedStorage;
    readonly CurrentSessionHolder sessionHolder;
    readonly MessageHandlerRegistry messageHandlerRegistry;
}
