using GumtreeScraper.Model.Interfaces;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GumtreeScraper.Model
{
    public class CarModel : IBaseModel
    {
        public int Id { get; set; }

        public DateTime DateAdded { get; set; }

        public string Name { get; set; }

        public int CarMakeId { get; set; }

        [ForeignKey("CarMakeId")]
        public virtual CarMake VirtualCarMake { get; set; }
    }
}
