using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GumtreeScraper
{
    public class Article
    {
        public string Link { get; set; }

        public string Title { get; set; }

        public string Location { get; set; }

        public string Description { get; set; }

        public int Year { get; set; }

        public int Mileage { get; set; }

        public string FuelType { get; set; }

        public int EngineSize { get; set; }

        public int Price { get; set; }
    }
}
