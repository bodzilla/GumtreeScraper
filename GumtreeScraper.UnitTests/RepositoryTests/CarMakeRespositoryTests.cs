using System;
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
    }
}