using System.Linq;
using MassTransit.Saga;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MassTransit.RavenDbIntegration
{
    public interface IFetchSagaRepository<TSaga> where TSaga: class, ISaga
    {
        Task<IList<TSaga>> FindWhere(Expression<Func<TSaga, bool>> filter);
        Task<TSaga> Load(Guid sagaId);
    }
}