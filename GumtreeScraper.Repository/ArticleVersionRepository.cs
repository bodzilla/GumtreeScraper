using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using GumtreeScraper.Model;
using GumtreeScraper.Repository.Interfaces;

namespace GumtreeScraper.Repository
{
    public class ArticleVersionRepository : IGenericRepository<ArticleVersion>
    {
        private readonly GenericRepository<ArticleVersion> _repository;

        public ArticleVersionRepository()
        {
            _repository = new GenericRepository<ArticleVersion>();
        }

        public IList<ArticleVersion> GetAll(params Expression<Func<ArticleVersion, object>>[] navigationProperties)
        {
            return _repository.GetAll(x => x.VirtualArticle);
        }

        public IList<ArticleVersion> GetList(Func<ArticleVersion, bool> where, params Expression<Func<ArticleVersion, object>>[] navigationProperties)
        {
            return _repository.GetList(where, navigationProperties);
        }

        public ArticleVersion GetByDesc(Func<ArticleVersion, bool> where, Expression<Func<ArticleVersion, object>> navigationProperty, Func<ArticleVersion, object> orderProperty)
        {
            return _repository.GetByDesc(where, navigationProperty, orderProperty);
        }

        public ArticleVersion Get(Func<ArticleVersion, bool> where, params Expression<Func<ArticleVersion, object>>[] navigationProperties)
        {
            return _repository.Get(where, navigationProperties);
        }

        public bool Exists(Func<ArticleVersion, bool> where)
        {
            return _repository.Exists(where);
        }

        public void Create(params ArticleVersion[] articleVersions)
        {
            // Check for duplicates before creating.
            foreach (ArticleVersion articleVersion in articleVersions)
            {
                //bool articleExists = Exists(x => x.Title == articleVersion.);
                //if (articleExists) throw new ArgumentException("Article exists.");
            }
            _repository.Create(articleVersions);
        }

        public void Update(params ArticleVersion[] articles)
        {
            // Check for duplicates before updating.
            foreach (ArticleVersion article in articles)
            {
                //bool articleExists = Exists(x => x.Link == article.Link);
                //if (articleExists) throw new ArgumentException("Article exists.");
            }
            _repository.Update(articles);
        }

        public void Delete(params ArticleVersion[] articles)
        {
            _repository.Delete(articles);
        }
    }
}
