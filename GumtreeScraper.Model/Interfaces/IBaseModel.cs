using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GumtreeScraper.Model.Interfaces
{
    public interface IBaseModel
    {
        int Id { get; set; }

        DateTime DateAdded { get; set; }
    }
}
