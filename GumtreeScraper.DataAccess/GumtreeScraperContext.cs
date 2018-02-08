using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
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

            modelBuilder.Entity<CarMake>().ToTable("CarMake");
            modelBuilder.Entity<CarModel>().ToTable("CarModel");
            modelBuilder.Entity<Article>().ToTable("Article");
            modelBuilder.Entity<ArticleVersion>().ToTable("ArticleVersion");
        }

        public DbSet<CarMake> CarMakes { get; set; }

        public DbSet<CarModel> CarModels { get; set; }

        public DbSet<Article> Articles { get; set; }

        public DbSet<ArticleVersion> ArticleVersions { get; set; }
    }
}
