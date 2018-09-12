using System;
using System.Threading.Tasks;
using NServiceBus;

class MyMessageHandler : IHandleMessages<MyMessage>
{
    readonly MyDataContext dataContext;

    public MyMessageHandler(MyDataContext dataContext)
    {
        this.dataContext = dataContext;
    }

    public async Task Handle(MyMessage message, IMessageHandlerContext context)
    {
        dataContext.MyEntities.Add(new MyEntity {Id = message.Id});

        await dataContext.SaveChangesAsync().ConfigureAwait(false);
    }
}