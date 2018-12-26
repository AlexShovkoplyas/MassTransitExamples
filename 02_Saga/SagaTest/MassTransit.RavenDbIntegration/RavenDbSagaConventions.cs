using System.Threading.Tasks;
using MassTransit.Saga;
using Raven.Abstractions.Util;
using Raven.Client;
using Raven.Client.Connection.Async;

namespace MassTransit.RavenDbIntegration
{
    public static class RavenDbSagaConventions
    {
        public static Task<string> GetSagaDocumentId(string dbName, IAsyncDatabaseCommands command, ISaga saga)
        {
            return new CompletedTask<string>($"{typeof (ISaga).Name}/{saga.CorrelationId}");
        }

        public static void RegisterSagaIdConvention(this IDocumentStore store)
        {
            store.Conventions.RegisterAsyncIdConvention<ISaga>(GetSagaDocumentId);
        }
    }
}