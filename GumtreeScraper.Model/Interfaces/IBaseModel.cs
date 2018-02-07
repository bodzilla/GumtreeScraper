using System;

namespace GumtreeScraper.Model.Interfaces
{
    public interface IBaseModel
    {
        int Id { get; set; }

        DateTime DateAdded { get; set; }
    }
}
