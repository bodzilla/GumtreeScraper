using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using GumtreeScraper.Model.Interfaces;

namespace GumtreeScraper.Repository.Interfaces
{
    public interface IGenericRepository<T> where T : IBaseModel
    {
        IList<T> GetAll(params Expression<Func<T, object>>[] navigationProperties);

        IList<T> GetList(Func<T, bool> where, params Expression<Func<T, object>>[] navigationProperties);

        T Get(Func<T, bool> where, params Expression<Func<T, object>>[] navigationProperties);

        bool Exists(Func<T, bool> where);

        void Create(params T[] users);

        void Update(params T[] items);

        void Delete(params T[] items);
    }
}
