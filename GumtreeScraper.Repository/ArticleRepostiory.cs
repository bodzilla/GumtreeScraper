using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using GumtreeScraper.Model;
using GumtreeScraper.Repository.Interfaces;

namespace GumtreeScraper.Repository
{
    public class ArticleRepository : IGenericRepository<Article>
    {
        private readonly GenericRepository<Article> _repository;

        public ArticleRepository()
        {
            _repository = new GenericRepository<Article>();
        }

        public IList<Article> GetAll(params Expression<Func<Article, object>>[] navigationProperties)
        {
            return _repository.GetAll(x => x.VirtualArticleVersions, x => x.VirtualCarModel);
        }

        public IList<Article> GetList(Func<Article, bool> where, params Expression<Func<Article, object>>[] navigationProperties)
        {
            return _repository.GetList(where, navigationProperties);
        }

        public Article Get(Func<Article, bool> where, params Expression<Func<Article, object>>[] navigationProperties)
        {
            return _repository.Get(where, navigationProperties);
        }

        public bool Exists(Func<Article, bool> where)
        {
            return _repository.Exists(where);
        }

        public void Create(params Article[] articles)
        {
            // Check for duplicates before creating.
            foreach (Article article in articles)
            {
                bool articleExists = Exists(x => x.Link == article.Link);
                if (articleExists) throw new ArgumentException("Article exists.");
            }
            _repository.Create(articles);
        }

        public void Update(params Article[] articles)
        {
            // Check for duplicates before updating.
            foreach (Article article in articles)
            {
                bool articleExists = Exists(x => x.Link == article.Link);
                if (articleExists) throw new ArgumentException("Article exists.");
            }
            _repository.Update(articles);
        }

        public void Delete(params Article[] articles)
        {
            _repository.Delete(articles);
        }
    }
}
