using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using GumtreeScraper.DataAccess.Migrations;
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
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<GumtreeScraperContext, Configuration>());

            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();

            modelBuilder.Entity<CarMake>().ToTable("CarMake");
            modelBuilder.Entity<CarModel>().ToTable("CarModel");
            modelBuilder.Entity<Article>().ToTable("Article");
            modelBuilder.Entity<ArticleVersion>().ToTable("ArticleVersion");
        }
    }
}
