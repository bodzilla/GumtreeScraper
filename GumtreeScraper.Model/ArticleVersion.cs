using GumtreeScraper.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GumtreeScraper.Model
{
    public class ArticleVersion : IBaseModel
    {
        public int Id { get; set; }

        public DateTime DateAdded { get; set; }

        public int ArticleId { get; set; }

        public int Version { get; set; }

        public string Title { get; set; }

        public string Location { get; set; }

        public string Description { get; set; }

        public int Year { get; set; }

        public int Mileage { get; set; }

        public string FuelType { get; set; }

        public int EngineSize { get; set; }

        public int Price { get; set; }

        [ForeignKey("ArticleId")]
        public virtual Article VirtualArticle { get; set; }
    }
}
