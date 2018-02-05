using GumtreeScraper.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GumtreeScraper.Model
{
    public class Article : IBaseModel
    {
        public Article()
        {
            VirtualArticleVersions = new HashSet<ArticleVersion>();
        }

        public int Id { get; set; }

        public DateTime DateAdded { get; set; }

        public int CarModelId { get; set; }

        [ForeignKey("CarModelId")]
        public virtual CarModel VirtualCarModel { get; set; }

        public virtual ICollection<ArticleVersion> VirtualArticleVersions { get; set; }
    }
}
