using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;

namespace GumtreeScraper
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly TimeSpan Timer = new TimeSpan(0, 1, 0); // 1 minute.

        static void Main(string[] args)
        {
            string url = "https://www.gumtree.com/search?search_category=cars&search_location=e10lj&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&vehicle_mileage=up_to_80000";

            // Set up driver.
            Log.Info("Initialising Gumtree Scraper..");
            PhantomJSDriverService service = PhantomJSDriverService.CreateDefaultService();
            service.AddArgument("--webdriver-loglevel=NONE");
            IWebDriver driver = new PhantomJSDriver(service);
            driver.Manage().Timeouts().ImplicitWait = Timer;

            Log.Info($"Scraping started on: {url}");
        }
    }
}
