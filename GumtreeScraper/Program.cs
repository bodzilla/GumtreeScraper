using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.UI;

namespace GumtreeScraper
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static IWebDriver _driver;

        static void Main(string[] args)
        {
            try
            {
                string url = "https://www.gumtree.com/search?search_category=cars&search_location=e10lj&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&vehicle_mileage=up_to_80000";

                // Set up driver.
                Log.Info("Initialising Gumtree Scraper..");
                PhantomJSDriverService service = PhantomJSDriverService.CreateDefaultService();
                service.AddArgument("--webdriver-loglevel=NONE");
                _driver = new PhantomJSDriver(service);
                WebDriverWait wait = new WebDriverWait(_driver, new TimeSpan(0, 1, 0));

                Log.Info($"Scraping started on: {url}");
                _driver.Url = url;
                wait.Until(d => d.FindElement(By.XPath("//*[@id=\"srp-results\"]/div[1]"))); // Results div.

                Log.Info("Loaded page 1.");
                IList<IWebElement> results = _driver.FindElements(By.XPath("//*[a[@class=\"listing-link\"]]"));
                IList<IWebElement> articles = new List<IWebElement>();

                foreach (var result in results)
                {
                    string xpath = GetElementXPath(result);
                    Log.Info(xpath);
                    articles.Add(_driver.FindElement(By.XPath(xpath)));

                }

                foreach (var article in articles)
                {
                    Log.Info(article.Text);
                }
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

        public static string GetElementXPath(IWebElement element)
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
