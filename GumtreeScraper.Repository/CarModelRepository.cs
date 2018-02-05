using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using GumtreeScraper.Model;
using GumtreeScraper.Repository.Interfaces;

namespace GumtreeScraper.Repository
{
    public class CarModelRepository : IGenericRepository<CarModel>
    {
        private readonly GenericRepository<CarModel> _repository;

        public CarModelRepository()
        {
            _repository = new GenericRepository<CarModel>();
        }

        public IList<CarModel> GetAll(params Expression<Func<CarModel, object>>[] navigationProperties)
        {
            return _repository.GetAll(x => x.VirtualCarMake);
        }

        public IList<CarModel> GetList(Func<CarModel, bool> where, params Expression<Func<CarModel, object>>[] navigationProperties)
        {
            return _repository.GetList(where, navigationProperties);
        }

        public CarModel Get(Func<CarModel, bool> where, params Expression<Func<CarModel, object>>[] navigationProperties)
        {
            return _repository.Get(where, navigationProperties);
        }

        public bool Exists(Func<CarModel, bool> where)
        {
            return _repository.Exists(where);
        }

        public void Create(params CarModel[] carModels)
        {
            // Check for duplicates before creating.
            foreach (CarModel carModel in carModels)
            {
                bool carModelExists = Exists(x => x.Name == carModel.Name);
                if (carModelExists) throw new ArgumentException("Car Model exists.");
            }
            _repository.Create(carModels);
        }

        public void Update(params CarModel[] carModels)
        {
            // Check for duplicates before updating.
            foreach (CarModel carModel in carModels)
            {
                bool carModelExists = Exists(x => x.Name == carModel.Name);
                if (carModelExists) throw new ArgumentException("Car Model exists.");
            }
            _repository.Update(carModels);
        }

        public void Delete(params CarModel[] carModels)
        {
            _repository.Delete(carModels);
        }
    }
}
