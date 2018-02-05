using GumtreeScraper.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
