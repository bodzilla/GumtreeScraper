﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using GumtreeScraper.DataAccess;
using GumtreeScraper.Model.Interfaces;
using GumtreeScraper.Repository.Interfaces;

namespace GumtreeScraper.Repository
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class, IBaseModel
    {
        public IList<T> GetAll(params Expression<Func<T, object>>[] navigationProperties)
        {
            IList<T> list;
            using (var context = new GumtreeScraperContext())
            {
                IQueryable<T> dbQuery = context.Set<T>();

                // Eager loading.
                foreach (Expression<Func<T, object>> navigationProperty in navigationProperties)
                {
                    dbQuery = dbQuery.Include(navigationProperty);
                }

                list = dbQuery
                    .AsNoTracking()
                    .ToList();
            }
            return list;
        }

        public IList<T> GetList(Func<T, bool> where,
             params Expression<Func<T, object>>[] navigationProperties)
        {
            IList<T> list;
            using (var context = new GumtreeScraperContext())
            {
                IQueryable<T> dbQuery = context.Set<T>();

                // Eager loading.
                foreach (Expression<Func<T, object>> navigationProperty in navigationProperties)
                {
                    dbQuery = dbQuery.Include(navigationProperty);
                }

                list = dbQuery
                    .AsNoTracking().AsEnumerable()
                    .Where(where)
                    .ToList();
            }
            return list;
        }

        public T GetByDesc(Func<T, bool> where, Expression<Func<T, object>> navigationProperty, Func<T, object> orderProperty)
        {
            T item;
            using (var context = new GumtreeScraperContext())
            {
                IQueryable<T> dbQuery = context.Set<T>();

                // Eager loading.
                dbQuery = dbQuery.Include(navigationProperty);

                item = dbQuery
                    .AsNoTracking().AsEnumerable() // Don't track any changes for the selected item.
                    .OrderByDescending(orderProperty) // Order by desc.
                    .FirstOrDefault(where); // Apply where clause.
            }
            return item;
        }

        public T Get(Func<T, bool> where, params Expression<Func<T, object>>[] navigationProperties)
        {
            T item;
            using (var context = new GumtreeScraperContext())
            {
                IQueryable<T> dbQuery = context.Set<T>();

                // Eager loading.
                foreach (Expression<Func<T, object>> navigationProperty in navigationProperties)
                {
                    dbQuery = dbQuery.Include(navigationProperty);
                }

                item = dbQuery
                    .AsNoTracking() // Don't track any changes for the selected item.
                    .FirstOrDefault(where); // Apply where clause.
            }
            return item;
        }

        public bool Exists(Func<T, bool> where)
        {
            return Get(where) != null;
        }

        public virtual void Create(T[] items)
        {
            using (var context = new GumtreeScraperContext())
            {
                foreach (T item in items)
                {
                    item.DateAdded = DateTime.Now;
                    context.Entry(item).State = EntityState.Added;
                }
                context.SaveChanges();
            }
        }

        public virtual void Update(params T[] items)
        {
            using (var context = new GumtreeScraperContext())
            {
                foreach (T item in items)
                {
                    context.Entry(item).State = EntityState.Modified;
                }
                context.SaveChanges();
            }
        }

        public virtual void Delete(params T[] items)
        {
            using (var context = new GumtreeScraperContext())
            {
                foreach (T item in items)
                {
                    context.Entry(item).State = EntityState.Deleted;
                }
                context.SaveChanges();
            }
        }
    }
}
