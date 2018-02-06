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
        private static readonly CarMakeRepository CarMakeRepo = new CarMakeRepository();
        private static readonly CarModelRepository CarModelRepo = new CarModelRepository();
        private static readonly ArticleRepository ArticleRepo = new ArticleRepository();
        private static readonly ArticleVersionRepository ArticleVersionRepo = new ArticleVersionRepository();
        private static IWebDriver _driver;
        private static WebDriverWait _wait;
        private static string _url;

        private static void Main(string[] args)
        {
            try
            {
                //CarMake carMake = new CarMake();
                //carMake.Name = "Renault";
                //CarMakeRepo.Create(carMake);

                //CarModel carModel = new CarModel();
                //carModel.CarMakeId = carMake.Id;
                //carModel.Name = "Clio";
                //CarModelRepo.Create(carModel);

                // Standardise article regex.
                Regex removeNonNumeric = new Regex(@"[^\d]");
                Regex removeLineBreaks = new Regex(@"\r\n?|\n");
                Regex removeExcessiveSpaces = new Regex(@"\s+");
                Regex removeRedunantString = new Regex(@".*Distance from search location.* \| ");

                // Set up driver.
                Log.Info("Initialising Gumtree Scraper..");
                PhantomJSDriverService service = PhantomJSDriverService.CreateDefaultService();
                service.AddArgument("--webdriver-loglevel=NONE");
                _driver = new PhantomJSDriver(service);
                _driver.Manage().Window.Maximize(); // To capture all content.

                _wait = new WebDriverWait(_driver, new TimeSpan(0, 0, Timeout));
                _url = "https://www.gumtree.com/search?search_category=cars&search_location=e10lj&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&vehicle_mileage=up_to_80000&photos_filter=true";

                // Scrape results by paging through.
                for (int i = 1; i <= Pages; i++)
                {
                    // Set page.
                    string currentPage = $"{_url}&page={i}";
                    Log.Info($"Scraping page: {currentPage}");
                    _driver.Url = currentPage;
                    _wait.Until(d => d.FindElement(By.XPath(@"//*[@id=""srp-results""]/div[1]"))); // Results div.

                    // Find articles and add links to list.
                    IList<IWebElement> results = _driver.FindElements(By.XPath(@"//*[a[@class=""listing-link""]]"));
                    foreach (IWebElement result in results)
                    {
                        try
                        {
                            string path = GetElementXPath(result);
                            string link = _driver.FindElement(By.XPath($"{path}/a")).GetAttribute("href").Trim();
                            string title = _driver.FindElement(By.XPath($"{path}/a/div[2]/h2")).Text.Trim();

                            // If link or title are empty, then this is not a valid article.
                            if (String.IsNullOrWhiteSpace(link) || String.IsNullOrWhiteSpace(title)) continue;

                            string location = _driver.FindElement(By.XPath($"{path}/a/div[2]/div[1]/span")).Text.Trim();
                            string description = _driver.FindElement(By.XPath($"{path}/a/div[2]/p")).Text.Trim();
                            string year = null;

                            try
                            {
                                year = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[1]/span[2]")).Text.Trim();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Could not get year.");
                            }

                            string mileage = null;

                            try
                            {
                                mileage = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[2]/span[2]")).Text.Trim();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Could not get mileage.");
                            }

                            string sellerType;
                            string fuelType;
                            string engineSize = null;

                            string tradeTypeOrfuelType = null;
                            string fuelTypeOrEngineSize = null;
                            string engineSizeOrNothing = null;

                            try
                            {
                                tradeTypeOrfuelType = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[3]/span[2]")).Text.Trim();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Could not get tradeTypeOrfuelType.");
                            }

                            try
                            {
                                fuelTypeOrEngineSize = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[4]/span[2]")).Text.Trim();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Could not get fuelTypeOrEngineSize.");
                            }

                            try
                            {
                                engineSizeOrNothing = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[5]/span[2]")).Text.Trim();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Could not get engineSizeOrNothing.");
                            }

                            if (tradeTypeOrfuelType != null && tradeTypeOrfuelType.Equals("Trade"))
                            {
                                sellerType = tradeTypeOrfuelType;
                                fuelType = fuelTypeOrEngineSize;
                                if (!String.IsNullOrWhiteSpace(engineSizeOrNothing)) engineSize = engineSizeOrNothing;
                            }
                            else
                            {
                                sellerType = "Private";
                                fuelType = tradeTypeOrfuelType;
                                if (!String.IsNullOrWhiteSpace(fuelTypeOrEngineSize)) engineSize = fuelTypeOrEngineSize;
                            }

                            string price = _driver.FindElement(By.XPath($"{path}/a/div[2]/span")).Text.Trim();

                            // Standardise result.
                            try
                            {
                                location = removeRedunantString.Replace(removeLineBreaks.Replace(location, " "), String.Empty);
                            }
                            catch (Exception ex)
                            {
                                // Ignore.
                            }

                            try
                            {
                                year = removeNonNumeric.Replace(year, String.Empty);
                            }
                            catch (Exception ex)
                            {
                                // Ignore.
                            }

                            try
                            {
                                mileage = removeNonNumeric.Replace(mileage, String.Empty);
                            }
                            catch (Exception ex)
                            {
                                // Ignore.
                            }

                            try
                            {
                                engineSize = removeNonNumeric.Replace(engineSize, String.Empty);
                            }
                            catch (Exception ex)
                            {
                                // Ignore.
                            }

                            try
                            {
                                price = removeNonNumeric.Replace(price, String.Empty);
                            }
                            catch (Exception ex)
                            {
                                // Ignore.
                            }

                            // Check if link exists in db.
                            Article dbArticle = null;
                            ArticleVersion dbArticleVersion = null;
                            bool articleLinkExists = ArticleRepo.Exists(x => x.Link == link);

                            if (articleLinkExists)
                            {
                                // Hash latest version of this article.
                                dbArticleVersion = ArticleVersionRepo.Get(x => x.VirtualArticle.Link == link, x => x.VirtualArticle);
                                dbArticle = dbArticleVersion.VirtualArticle;

                                byte[] dbtitleBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Title);
                                byte[] dblocationBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Location);
                                byte[] dbdescriptionBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Description);

                                byte[] dbYearBytes = { };
                                try
                                {
                                    dbYearBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Year.ToString());
                                }
                                catch (Exception)
                                {
                                    // Ignore. 
                                }

                                byte[] dbmileageBytes = { };
                                try
                                {
                                    dbmileageBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Mileage.ToString());
                                }
                                catch (Exception)
                                {
                                    // Ignore. 
                                }

                                byte[] dbSellerTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.SellerType);
                                byte[] dbfuelTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.FuelType);

                                byte[] dbengineSizeBytes = { };
                                try
                                {
                                    dbengineSizeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.EngineSize.ToString());
                                }
                                catch (Exception)
                                {
                                    // Ignore. 
                                }

                                byte[] dbpriceBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Price.ToString());
                                byte[] dbBytes = CombineBytes(dbtitleBytes, dblocationBytes, dbdescriptionBytes,
                                    dbYearBytes, dbmileageBytes, dbSellerTypeBytes, dbfuelTypeBytes, dbengineSizeBytes, dbpriceBytes);
                                string dbHash = GenerateHash(dbBytes);

                                // Hash fetched verison of this article.
                                byte[] titleBytes = Encoding.ASCII.GetBytes(title);
                                byte[] locationBytes = Encoding.ASCII.GetBytes(location);
                                byte[] descriptionBytes = Encoding.ASCII.GetBytes(description);

                                byte[] yearBytes = { };
                                try
                                {
                                    yearBytes = Encoding.ASCII.GetBytes(year);
                                }
                                catch (Exception)
                                {
                                    // Ignore. 
                                }

                                byte[] mileageBytes = { };
                                try
                                {
                                    mileageBytes = Encoding.ASCII.GetBytes(mileage);
                                }
                                catch (Exception)
                                {
                                    // Ignore. 
                                }

                                byte[] sellerTypeBytes = Encoding.ASCII.GetBytes(sellerType);
                                byte[] fuelTypeBytes = Encoding.ASCII.GetBytes(fuelType);

                                byte[] engineSizeBytes = { };
                                try
                                {
                                    engineSizeBytes = Encoding.ASCII.GetBytes(engineSize);
                                }
                                catch (Exception)
                                {
                                    // Ignore. 
                                }

                                byte[] priceBytes = Encoding.ASCII.GetBytes(price);
                                byte[] bytes = CombineBytes(titleBytes, locationBytes, descriptionBytes,
                                    yearBytes, mileageBytes, sellerTypeBytes, fuelTypeBytes, engineSizeBytes, priceBytes);
                                string hash = GenerateHash(bytes);

                                // Skip if hashes are the same.
                                if (String.Equals(dbHash, hash)) continue;
                            }

                            Article article = new Article();
                            ArticleVersion articleVersion = new ArticleVersion();
                            if (dbArticle == null)
                            {
                                // New article.
                                article.Link = link;
                                article.CarModelId = CarModelRepo.Get(x => x.Name.Equals("Clio")).Id;
                                ArticleRepo.Create(article);
                                articleVersion.ArticleId = article.Id;
                                articleVersion.Version = 1; // Set version.
                            }
                            else
                            {
                                articleVersion.ArticleId = dbArticle.Id;
                                articleVersion.Version = dbArticleVersion.Version + 1; // Increment version.
                            }

                            // New article version.
                            articleVersion.Title = title;
                            articleVersion.Location = location;
                            articleVersion.Description = description;
                            articleVersion.Year = year != null ? int.Parse(year) : (int?)null;
                            articleVersion.Mileage = mileage != null ? int.Parse(mileage) : (int?)null;
                            articleVersion.SellerType = sellerType;
                            articleVersion.FuelType = fuelType;
                            articleVersion.EngineSize = engineSize != null ? int.Parse(engineSize) : (int?)null;
                            articleVersion.Price = int.Parse(price);
                            ArticleVersionRepo.Create(articleVersion);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.GetBaseException());
                        }
                    }
                }
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
