using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GumtreeScraper.Model;

namespace GumtreeScraper.DataAccess
{
    public class GumtreeScraperContext : DbContext
    {
        public GumtreeScraperContext()
            : base("GumtreeScraperConnection")
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();

            modelBuilder.Entity<CarMake>().ToTable("CarMakes");
            modelBuilder.Entity<CarModel>().ToTable("CarModels");
            modelBuilder.Entity<Article>().ToTable("Articles");
            modelBuilder.Entity<ArticleVersion>().ToTable("ArticleVersions");
        }

        public DbSet<CarMake> CarMakes { get; set; }

        public DbSet<CarModel> CarModels { get; set; }

        public DbSet<Article> Articles { get; set; }

        public DbSet<ArticleVersion> ArticleVersions { get; set; }
    }
}
