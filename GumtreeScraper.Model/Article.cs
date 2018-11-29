using GumtreeScraper.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

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

        public int? DaysOld { get; set; }

        public bool Active { get; set; } = true;

        public string Link { get; set; }

        public string Thumbnail { get; set; }

        public int MediaCount { get; set; } = 0;

        public int CarModelId { get; set; }

        [ForeignKey("CarModelId")]
        public virtual CarModel VirtualCarModel { get; set; }

        public virtual ICollection<ArticleVersion> VirtualArticleVersions { get; set; }
    }
}
