using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GreenPipes;
using MassTransit.Logging;
using MassTransit.Saga;
using MassTransit.Util;
using Raven.Client;
using Raven.Client.Linq;

namespace MassTransit.RavenDbIntegration
{
    public class RavenDbSagaRepository<TSaga> : ISagaRepository<TSaga>, IQuerySagaRepository<TSaga>,
        IFetchSagaRepository<TSaga>
        where TSaga : class, ISaga
    {
        private static readonly ILog _log = Logger.Get<RavenDbSagaRepository<TSaga>>();
        private readonly IDocumentStore _store;

        public RavenDbSagaRepository(IDocumentStore store)
        {
            _store = store;
        }

        private IAsyncDocumentSession OpenSession()
        {
            var session = _store.OpenAsyncSession();
            session.Advanced.UseOptimisticConcurrency = true;
            return session;
        }

        public async Task<IEnumerable<Guid>> Find(ISagaQuery<TSaga> query)
        {
            using (var session = OpenSession())
            {
                return await session.Query<TSaga>()
                    .Where(query.FilterExpression)
                    .Customize(x => x.WaitForNonStaleResultsAsOf(DateTime.Now + TimeSpan.FromMinutes(1)))
                    .Select(x => x.CorrelationId)
                    .ToListAsync();
            }
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateScope("sagaRepository");
            scope.Set(new
            {
                Persistence = "ravenDb"
            });
        }

        async Task ISagaRepository<TSaga>.Send<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next)
        {
            if (!context.CorrelationId.HasValue)
                throw new SagaException("The CorrelationId was not specified", typeof(TSaga), typeof(T));

            var sagaId = context.CorrelationId.Value;
            using (var session = OpenSession())
            {
                var inserted = false;
                TSaga instance;

                if (policy.PreInsertInstance(context, out instance))
                    inserted = await PreInsertSagaInstance<T>(session, instance);

                try
                {
                    if (instance == null)
                        instance = await session.LoadAsync<TSaga>(ConvertToSagaId(session, sagaId))
                            .ConfigureAwait(false);

                    if (instance == null)
                    {
                        var missingSagaPipe = new MissingPipe<T>(session, next);
                        await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
                    }
                    else
                    {
                        if (_log.IsDebugEnabled)
                        {
                            _log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName,
                                instance.CorrelationId, TypeMetadataCache<T>.ShortName);
                        }
                        var sagaConsumeContext = new RavenDbSagaConsumeContext<TSaga, T>(session, context, instance);

                        await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);

                        if (inserted && !sagaConsumeContext.IsCompleted)
                            await session.StoreAsync(instance, GetSagaId(session, instance)).ConfigureAwait(false);

                        if (_log.IsDebugEnabled)
                            _log.DebugFormat("SAGA (Send): New saga state: {@Saga}", instance);
                    }
                    await session.SaveChangesAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (_log.IsErrorEnabled)
                        _log.Error(
                            $"SAGA:{TypeMetadataCache<TSaga>.ShortName} Exception {TypeMetadataCache<T>.ShortName}",
                            ex);

                    throw;
                }
            }
        }

        public async Task SendQuery<T>(SagaQueryConsumeContext<TSaga, T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            using (var session = OpenSession())
            {
                try
                {
                    var guids = (await Find(context.Query).ConfigureAwait(false)).ToArray();

                    if (!guids.Any())
                    {
                        var missingSagaPipe = new MissingPipe<T>(session, next);
                        await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
                    }
                    else
                    {
                        var ids = guids.Select(x => ConvertToSagaId(session, x)).ToArray();
                        var instances = await session.LoadAsync<TSaga>(ids);
                        foreach (var instance in instances)
                            await SendToInstance(context, policy, instance, next, session).ConfigureAwait(false);
                    }
                    await session.SaveChangesAsync();
                }
                catch (SagaException sex)
                {
                    if (_log.IsErrorEnabled)
                        _log.Error("Saga Exception Occurred", sex);

                    throw;
                }
                catch (Exception ex)
                {
                    if (_log.IsErrorEnabled)
                        _log.Error(
                            $"SAGA:{TypeMetadataCache<TSaga>.ShortName} Exception {TypeMetadataCache<T>.ShortName}",
                            ex);

                    throw new SagaException(ex.Message, typeof(TSaga), typeof(T), Guid.Empty, ex);
                }
            }
        }

        private static async Task<bool> PreInsertSagaInstance<T>(IAsyncDocumentSession session, TSaga instance)
        {
            try
            {
                await session.StoreAsync(instance, GetSagaId(session, instance));
                await session.SaveChangesAsync();

                _log.DebugFormat("SAGA:{0}:{1} Insert {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId,
                    TypeMetadataCache<T>.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                if (_log.IsDebugEnabled)
                {
                    _log.DebugFormat("SAGA:{0}:{1} Dupe {2} - {3}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName, ex.Message);
                }
                return false;
            }
        }

        private static string ConvertToSagaId(IAsyncDocumentSession session, Guid guid)
            => session.Advanced.DocumentStore.Conventions.FindFullDocumentKeyFromNonStringIdentifier(guid,
                typeof(TSaga), false);

        private static string GetSagaId(IAsyncDocumentSession session, TSaga instance)
            => session.Advanced.DocumentStore.Conventions.FindFullDocumentKeyFromNonStringIdentifier(
                instance.CorrelationId, instance.GetType(), false);

        private static async Task SendToInstance<T>(SagaQueryConsumeContext<TSaga, T> context,
            ISagaPolicy<TSaga, T> policy, TSaga instance,
            IPipe<SagaConsumeContext<TSaga, T>> next, IAsyncDocumentSession session)
            where T : class
        {
            try
            {
                if (_log.IsDebugEnabled)
                {
                    _log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName);
                }
                var sagaConsumeContext = new RavenDbSagaConsumeContext<TSaga, T>(session, context, instance);

                await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);
            }
            catch (SagaException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SagaException(ex.Message, typeof(TSaga), typeof(T), instance.CorrelationId, ex);
            }
        }

        /// <summary>
        ///     Once the message pipe has processed the saga instance, add it to the saga repository
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        private class MissingPipe<TMessage> :
            IPipe<SagaConsumeContext<TSaga, TMessage>>
            where TMessage : class
        {
            private readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;
            private readonly IAsyncDocumentSession _session;

            public MissingPipe(IAsyncDocumentSession session, IPipe<SagaConsumeContext<TSaga, TMessage>> next)
            {
                _session = session;
                _next = next;
            }

            void IProbeSite.Probe(ProbeContext context)
            {
                _next.Probe(context);
            }

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                if (_log.IsDebugEnabled)
                {
                    _log.DebugFormat("SAGA:{0}:{1} Added {2}", TypeMetadataCache<TSaga>.ShortName,
                        context.Saga.CorrelationId,
                        TypeMetadataCache<TMessage>.ShortName);
                }

                SagaConsumeContext<TSaga, TMessage> proxy = new RavenDbSagaConsumeContext<TSaga, TMessage>(_session,
                    context, context.Saga);

                await _next.Send(proxy).ConfigureAwait(false);

                if (!proxy.IsCompleted)
                    await _session.StoreAsync(context.Saga, GetSagaId(_session, context.Saga));
            }
        }

        public async Task<IList<TSaga>> FindWhere(Expression<Func<TSaga, bool>> filter)
        {
            using (var session = _store.OpenAsyncSession())
                return await session.Query<TSaga>().Where(filter).ToListAsync();
        }

        public async Task<TSaga> Load(Guid sagaId)
        {
            using (var session = _store.OpenAsyncSession())
                return await session.LoadAsync<TSaga>(ConvertToSagaId(session, sagaId));
        }
    }
}