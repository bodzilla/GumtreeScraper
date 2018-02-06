using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GumtreeScraper.Model;
using log4net;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.UI;
using GumtreeScraper.Repository;

namespace GumtreeScraper
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly int Timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutSecs"]);
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
                _driver.Manage().Window.Maximize(); // To capture all content.

                _wait = new WebDriverWait(_driver, new TimeSpan(0, 0, Timeout));
                _url = "https://www.gumtree.com/search?search_category=cars&search_location=e10lj&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&vehicle_mileage=up_to_80000";

                // Scrape results by paging through.
                for (int i = 1; i <= Pages; i++)
                {
                    // Set page.
                    string currentPage = $"{_url}&page={i}";
                    Log.Info($"Scraping page: {currentPage}");
                    _driver.Url = currentPage;
                    _wait.Until(d => d.FindElement(By.XPath(@"//*[@id=""srp-results""]/div[1]"))); // Results div.

                    IList<IWebElement> results = _driver.FindElements(By.XPath(@"//*[a[@class=""listing-link""]]"));
                    IList<string> links = new List<string>();

                    // Find articles and add links to list.
                    foreach (IWebElement result in results)
                    {
                        try
                        {
                            string path = GetElementXPath(result);
                            string link = _driver.FindElement(By.XPath($"{path}/a")).GetAttribute("href").Trim();
                            string title = _driver.FindElement(By.XPath($"{path}/a/div[2]/h2")).Text.Trim().ToLower();

                            // If link and title are empty, then this is not a valid article.
                            if (String.IsNullOrWhiteSpace(link) && String.IsNullOrWhiteSpace(title)) continue;

                            string location = _driver.FindElement(By.XPath($"{path}/a/div[2]/div[1]/span")).Text.Trim().ToLower();
                            string description = _driver.FindElement(By.XPath($"{path}/a/div[2]/p")).Text.Trim().ToLower();
                            string year = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[1]/span[2]")).Text.Trim();
                            string mileage = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[2]/span[2]")).Text.Trim().ToLower();
                            string fuelType = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[3]/span[2]")).Text.Trim().ToLower();
                            string engineSize = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[4]/span[2]")).Text.Trim();
                            string price = _driver.FindElement(By.XPath($"{path}/a/div[2]/span")).Text.Trim();

                            // Check if link exists in db.
                            ArticleRepository articleRepo = new ArticleRepository();
                            bool exists = articleRepo.Exists(x => x.Link == link);
                            if (exists)
                            {
                                ArticleVersionRepository articleVersionRepo = new ArticleVersionRepository();
                                ArticleVersion latestVersion = articleVersionRepo.Get(x => x.VirtualArticle.Link == link, x => x.Version);
                                byte[] titleBytes = Encoding.ASCII.GetBytes(latestVersion.Title);
                                byte[] locationBytes = Encoding.ASCII.GetBytes(latestVersion.Location);
                                byte[] descriptionBytes = Encoding.ASCII.GetBytes(latestVersion.Description);
                                byte[] yearBytes = Encoding.ASCII.GetBytes(latestVersion.Year.ToString());
                                byte[] mileageBytes = Encoding.ASCII.GetBytes(latestVersion.Mileage.ToString());
                                byte[] fuelTypeBytes = Encoding.ASCII.GetBytes(latestVersion.FuelType);
                                byte[] engineSizeBytes = Encoding.ASCII.GetBytes(latestVersion.EngineSize.ToString());
                                byte[] priceBytes = Encoding.ASCII.GetBytes(latestVersion.Price.ToString());
                                byte[] dbBytes = CombineBytes(titleBytes, locationBytes, descriptionBytes, yearBytes, mileageBytes, fuelTypeBytes, engineSizeBytes, priceBytes);
                                string dbHash = GenerateHash(dbBytes);

                                // TODO: Compare dbHash agaisnt current hash for new version.
                            }

                            // TODO: New article version.

                            // Set up regex.
                            Regex removeNonNumeric = new Regex(@"[^\d]");
                            Regex removeLineBreaks = new Regex(@"\r\n?|\n");
                            Regex removeExcessiveSpaces = new Regex(@"\s+");
                            Regex removeString = new Regex(@".*distance from search location.*miles ");

                            // TODO: Standardise the result set.
                        }
                        catch (Exception) { } // Ignore.
                    }
                }
                // TODO: List the results.
            }
            catch (Exception ex)
            {
                Log.Fatal(ex.GetBaseException());
            }
            finally
            {
                Log.Info("Gumtree Scraper finished session.");
                _driver.Quit();
            }
        }

        private static string GetElementXPath(IWebElement element)
        {
            const string javaScript = @"function getElementXPath(elt){var path = """";for (; elt && elt.nodeType == 1; elt = elt.parentNode){idx = getElementIdx(elt);xname = elt.tagName;if (idx > 1){xname += ""["" + idx + ""]"";}path = ""/"" + xname + path;}return path;}function getElementIdx(elt){var count = 1;for (var sib = elt.previousSibling; sib;sib = sib.previousSibling){if(sib.nodeType == 1 && sib.tagName == elt.tagName){count++;}}return count;}return getElementXPath(arguments[0]).toLowerCase();";
            return (string)((IJavaScriptExecutor)_driver).ExecuteScript(javaScript, element);
        }

        private static string CleanText(string text)
        {
            HashSet<char> removeChars = new HashSet<char>(@"?&^$#@!()+-,:;<>’\|'-_*");
            StringBuilder cleanText = new StringBuilder(text.Length);
            foreach (char c in text) if (!removeChars.Contains(c)) cleanText.Append(c);
            return cleanText.ToString();
        }

        private static string GenerateHash(byte[] data)
        {
            StringBuilder hash = new StringBuilder();

            // Use input string to calculate MD5 hash.
            MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(data);

            // Convert the byte array to hexadecimal string.
            foreach (byte _byte in hashBytes) hash.Append(_byte.ToString("X2"));
            return $"0x{hash}";
        }

        private static byte[] CombineBytes(params byte[][] arrays)
        {
            // Combine multiple byte arrays.
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
    }
}
