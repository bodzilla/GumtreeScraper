using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;

namespace GumtreeScraper
{
    class Program
    {
        private static readonly TimeSpan Timer = new TimeSpan(0, 1, 0); // 1 minute.

        static void Main(string[] args)
        {
            string url = args[0];

            // Set up driver.
            PhantomJSDriverService service = PhantomJSDriverService.CreateDefaultService();
            service.AddArgument("--webdriver-loglevel=NONE");
            IWebDriver driver = new PhantomJSDriver(service);

            // Time to wait if page is still loading.
            driver.Manage().Timeouts().ImplicitWait = Timer;

            Console.WriteLine("Started checks on " + url.Substring(7, 21));
        }
    }
}
