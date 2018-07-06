using System.Data.Entity.Migrations;

namespace GumtreeScraper.DataAccess.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<GumtreeScraperContext>
    {
        public Configuration()
        {
            ContextKey = typeof(GumtreeScraperContext).FullName;
            AutomaticMigrationsEnabled = true;
        }

        protected override void Seed(GumtreeScraperContext context)
        {
            //  This method will be called after migrating to the latest version.
            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data.
        }
    }
}
