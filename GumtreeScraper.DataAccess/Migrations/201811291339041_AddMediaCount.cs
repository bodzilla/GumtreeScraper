namespace GumtreeScraper.DataAccess.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMediaCount : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Article", "MediaCount", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Article", "MediaCount");
        }
    }
}
