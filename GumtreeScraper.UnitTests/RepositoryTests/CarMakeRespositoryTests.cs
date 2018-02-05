using System;
using System.Data.Entity.Infrastructure;
using GumtreeScraper.Model;
using GumtreeScraper.Repository;
using NUnit.Framework;

namespace GumtreeScraper.UnitTests.RepositoryTests
{
    [TestFixture]
    public class CarMakeRespositoryTests
    {
        [Test, Order(1)]
        public void CreateNewCarMake_CreateSuccessful_ReturnsPass()
        {
            // Arrange.
            CarMakeRepository carMakeRepo = new CarMakeRepository();
            CarMake carMake = new CarMake();
            carMake.Name = "Renault";

            // Act.
            carMakeRepo.Create(carMake);

            // Assert
            Assert.Pass();
        }

        [Test, Order(2)]
        public void CreateExistingCarMake_CreateFail_ReturnsPass()
        {
            // Create the same CarMake, should fail.
            Assert.Throws<ArgumentException>(CreateNewCarMake_CreateSuccessful_ReturnsPass);
        }

        [Test, Order(3)]
        public void CreateNewCarModel_CreateSuccessful_ReturnsPass()
        {
            // Arrange.
            CarMakeRepository carMakeRepo = new CarMakeRepository();
            CarModelRepository carModelRepo = new CarModelRepository();
            CarMake carMake = carMakeRepo.Get(x => x.Name.Equals("Renault"));
            CarModel carModel = new CarModel();
            carModel.CarMakeId = carMake.Id;
            carModel.Name = "Clio";

            // Act.
            carModelRepo.Create(carModel);

            // Assert.
            Assert.Pass();
        }

        [Test, Order(4)]
        public void CreateExistingCarModel_CreateFail_ReturnsPass()
        {
            // Create the same CarModel, should fail.
            Assert.Throws<ArgumentException>(CreateNewCarModel_CreateSuccessful_ReturnsPass);
        }

        [Test, Order(5)]
        public void UpdateCarMake_UpdateSuccessful_ReturnsPass()
        {
            // Arrange.
            CarMakeRepository carMakeRepo = new CarMakeRepository();
            CarMake carMake = carMakeRepo.Get(x => x.Name.Equals("Renault"));

            // Act.
            carMake.Name = "Ferrari";
            carMakeRepo.Update(carMake);

            // Assert.
            Assert.Pass();
        }

        [Test, Order(6)]
        public void UpdateCarModel_UpdateSuccessful_ReturnsPass()
        {
            // Arrange.
            CarModelRepository carModelRepo = new CarModelRepository();
            CarModel carModel = carModelRepo.Get(x => x.Name.Equals("Clio"));

            // Act.
            carModel.Name = "Enzo";
            carModelRepo.Update(carModel);

            // Assert.
            Assert.Pass();
        }

        [Test, Order(7)]
        public void DeleteCarMake_DeleteFail_ReturnsPass()
        {
            // Delete a CarMake when CarModels associated exist, should fail.
            Assert.Throws<DbUpdateException>(DeleteCarMake_DeleteSuccessful_ReturnsPass);
        }

        [Test, Order(8)]
        public void DeleteCarModel_DeleteFail_ReturnsPass()
        {
            // Arrange.
            CarModelRepository carModelRepo = new CarModelRepository();
            CarModel carModel = carModelRepo.Get(x => x.Name.Equals("Enzo"));

            // Act.
            carModelRepo.Delete(carModel);

            // Assert.
            Assert.Pass();
        }

        [Test, Order(9)]
        public void DeleteCarMake_DeleteSuccessful_ReturnsPass()
        {
            // Arrange.
            CarMakeRepository carMakeRepo = new CarMakeRepository();
            CarMake carMake = carMakeRepo.Get(x => x.Name.Equals("Ferrari"));

            // Act.
            carMakeRepo.Delete(carMake);

            // Assert.
            Assert.Pass();
        }
    }
}