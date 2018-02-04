using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using log4net;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.UI;

namespace GumtreeScraper
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly int Pages = int.Parse(ConfigurationManager.AppSettings["Pages"]);
        private static IWebDriver _driver;
        private static WebDriverWait _wait;
        private static string _url;

        private static void Main(string[] args)
        {
            try
            {
                // Set up driver.
                Log.Info("Initialising Gumtree Scraper..");
                PhantomJSDriverService service = PhantomJSDriverService.CreateDefaultService();
                service.AddArgument("--webdriver-loglevel=NONE");
                _driver = new PhantomJSDriver(service);
                _wait = new WebDriverWait(_driver, new TimeSpan(0, 1, 0));

                // Set page.
                _url = "https://www.gumtree.com/search?search_category=cars&search_location=e10lj&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&vehicle_mileage=up_to_80000&page=";

                // Scrape results by paging through.
                ScrapePages();

                // TODO: Cleanse data.
            }
            catch (Exception ex)
            {
                Log.Fatal(ex.GetBaseException());
            }
            finally
            {
                Log.Info("Gumtree Scraper finished session.");
                Environment.Exit(0);
            }
        }

        private static void ScrapePages()
        {
            for (int i = 1; i <= Pages; i++)
            {
                string currentPage = _url + i;
                Log.Info($"Scraping page: {currentPage}");
                _driver.Url = currentPage;
                _wait.Until(d => d.FindElement(By.XPath("//*[@id=\"srp-results\"]/div[1]"))); // Results div.

                // Scrape.
                IList<IWebElement> articles = _driver.FindElements(By.XPath("//*[a[@class=\"listing-link\"]]"));

                foreach (IWebElement article in articles)
                {
                    string xpath = GetElementXPath(article);
                    string link = _driver.FindElement(By.XPath($"{xpath}/a")).GetAttribute("href");
                    string title = _driver.FindElement(By.XPath($"{xpath}/a/div[2]/h2")).Text;
                    if (String.IsNullOrWhiteSpace(link) && String.IsNullOrWhiteSpace(title)) continue;
                }
            }
        }

        private static string GetElementXPath(IWebElement element)
        {
            const string javaScript = "function getElementXPath(elt){" +
                                      "var path = \"\";" +
                                      "for (; elt && elt.nodeType == 1; elt = elt.parentNode){" +
                                      "idx = getElementIdx(elt);" +
                                      "xname = elt.tagName;" +
                                      "if (idx > 1){" +
                                      "xname += \"[\" + idx + \"]\";" +
                                      "}" +
                                      "path = \"/\" + xname + path;" +
                                      "}" +
                                      "return path;" +
                                      "}" +
                                      "function getElementIdx(elt){" +
                                      "var count = 1;" +
                                      "for (var sib = elt.previousSibling; sib ; sib = sib.previousSibling){" +
                                      "if(sib.nodeType == 1 && sib.tagName == elt.tagName){" +
                                      "count++;" +
                                      "}" +
                                      "}" +
                                      "return count;" +
                                      "}" +
                                      "return getElementXPath(arguments[0]).toLowerCase();";
            return (string)((IJavaScriptExecutor)_driver).ExecuteScript(javaScript, element);
        }
    }
}
