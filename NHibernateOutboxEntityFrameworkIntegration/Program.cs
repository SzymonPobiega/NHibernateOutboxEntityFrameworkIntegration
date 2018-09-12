using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using NHibernate.Cfg;
using NHibernate.Connection;
using NServiceBus;
using NServiceBus.Persistence;
using NServiceBus.Unicast;

namespace NHibernateOutboxEntityFrameworkIntegration
{
    class Program
    {
        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var config = new EndpointConfiguration("NHibernateOutboxEntityFrameworkIntegration");
            config.EnableOutbox();
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();
            var currentSessionHolder = new CurrentSessionHolder();

            //we replace the regular LoadHandlersConnector with one that stores the NH session in the async local
            config.Pipeline.Replace("LoadHandlersConnector", b =>
            {
                var adapter = b.Build<ISynchronizedStorageAdapter>();
                var syncStorage = b.Build<ISynchronizedStorage>();

                return new MyLoadHandlersConnector(currentSessionHolder, b.Build<MessageHandlerRegistry>(), syncStorage, adapter);
            });

            config.RegisterComponents(c =>
            {
                c.ConfigureComponent<MyDataContext>(b =>
                {
                    var session = currentSessionHolder.Current;
                    var connection = session.Session().Connection;
                    var dataContext = new MyDataContext(connection);

                    DbTransaction transaction;

                    using (var dummyCommand = connection.CreateCommand())
                    {
                        session.Session().Transaction.Enlist(dummyCommand);
                        transaction = dummyCommand.Transaction;
                    }

                    dataContext.Database.UseTransaction(transaction);
                    return dataContext;

                }, DependencyLifecycle.InstancePerCall);
            });

            //connection.isolation instructs NHibernate to use Snapshot isolation level when opening transactions
            var persistence = config.UsePersistence<NHibernatePersistence>();
            var connectionString = "data source=(local); initial catalog=testnh; integrated security=true";
            var nhConfig = new Configuration
            {
                Properties =
                {
                    ["dialect"] = "NHibernate.Dialect.MsSql2012Dialect",
                    ["connection.driver_class"] = "NHibernate.Driver.Sql2008ClientDriver",
                    ["connection.connection_string"] = connectionString,
                    ["connection.isolation"] = IsolationLevel.Snapshot.ToString()
                }
            };

            persistence.UseConfiguration(nhConfig);

            config.UseTransport<LearningTransport>();
            var endpoint = await Endpoint.Start(config);

            var ctx = new MyDataContext(new SqlConnection(connectionString));
            ctx.Database.Initialize(true);

            Console.WriteLine("Type ID and press <enter> to create a new entity");
            while (true)
            {
                var id = Console.ReadLine();

                var message = new MyMessage
                {
                    Id = id
                };
                await endpoint.SendLocal(message).ConfigureAwait(false);
            }
        }


    }
}