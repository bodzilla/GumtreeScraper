using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.UI;
using GumtreeScraper.Model;
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

        private static readonly Regex RemoveNonNumeric = new Regex(@"[^\d]");
        private static readonly Regex RemoveLineBreaks = new Regex(@"\r\n?|\n");
        private static readonly Regex RemoveExcessLocationText = new Regex(@".* \| ");

        private static string _cakeMake;
        private static string _carModel;
        private static int _carModelId;

        private static IWebDriver _driver;
        private static WebDriverWait _wait;
        private static string _url;

        private static void Main(string[] args)
        {
            try
            {
                Log.Info("Retrieving runtime variables..");

                // Setting up initial vars.
                _cakeMake = ToTitleCase(args[0]);
                _carModel = ToTitleCase(args[1]);
                _url = args[2];

                // Check if car make and model exist in db.
                bool carMakeExists = CarMakeRepo.Exists(x => x.Name.Equals(_cakeMake, StringComparison.CurrentCultureIgnoreCase));
                bool carModelExists = CarModelRepo.Exists(x => x.Name.Equals(_carModel, StringComparison.CurrentCultureIgnoreCase));

                // Create/get and set ids.
                if (!carMakeExists) CarMakeRepo.Create(new CarMake { Name = _cakeMake });
                if (!carModelExists) CarModelRepo.Create(new CarModel { Name = _carModel, CarMakeId = CarMakeRepo.Get(x => x.Name.Equals(_cakeMake)).Id });
                _carModelId = CarModelRepo.Get(x => x.Name.Equals(_carModel)).Id;

                // Set up driver.
                Log.Info("Initialising Gumtree Scraper..");
                PhantomJSDriverService service = PhantomJSDriverService.CreateDefaultService();
                service.AddArgument("--webdriver-loglevel=NONE");
                _driver = new PhantomJSDriver(service);
                _driver.Manage().Window.Maximize(); // To capture all content.

                _wait = new WebDriverWait(_driver, new TimeSpan(0, 0, Timeout));

                // Scrape results by paging through.
                for (int i = 1; i <= Pages; i++)
                {
                    // Set page.
                    string currentPage = $"{_url}&page={i}";
                    Log.Info($"Scraping page: {currentPage}");
                    _driver.Url = currentPage;
                    _wait.Until(d => d.FindElement(By.XPath(@"//*[@id=""srp-results""]/div[1]"))); // Results div.

                    // Find article results.
                    IList<IWebElement> results = _driver.FindElements(By.XPath(@"//*[a[@class=""listing-link""]]"));
                    foreach (IWebElement result in results)
                    {
                        try
                        {
                            // Get article values.
                            string path = GetElementXPath(result);
                            string link = _driver.FindElement(By.XPath($"{path}/a")).GetAttribute("href").Trim();
                            string title = _driver.FindElement(By.XPath($"{path}/a/div[2]/h2")).Text.Trim();

                            // If link or title are empty, this is not a valid article.
                            if (String.IsNullOrWhiteSpace(link) || String.IsNullOrWhiteSpace(title)) continue;

                            string location = _driver.FindElement(By.XPath($"{path}/a/div[2]/div[1]/span")).Text.Trim();
                            string description = _driver.FindElement(By.XPath($"{path}/a/div[2]/p")).Text.Trim();

                            // Try catch these values as some may not exist.
                            string year = null;
                            try { year = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[1]/span[2]")).Text.Trim(); } catch (Exception) { Log.Debug("Could not get year."); }

                            string mileage = null;
                            try { mileage = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[2]/span[2]")).Text.Trim(); } catch (Exception) { Log.Debug("Could not get mileage."); }

                            string tradeTypeOrfuelType = null;
                            try { tradeTypeOrfuelType = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[3]/span[2]")).Text.Trim(); } catch (Exception) { Log.Debug("Could not get tradeTypeOrfuelType."); }

                            string fuelTypeOrEngineSize = null;
                            try { fuelTypeOrEngineSize = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[4]/span[2]")).Text.Trim(); } catch (Exception) { Log.Debug("Could not get fuelTypeOrEngineSize."); }

                            string engineSizeOrNothing = null;
                            try { engineSizeOrNothing = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[5]/span[2]")).Text.Trim(); } catch (Exception) { Log.Debug("Could not get engineSizeOrNothing."); }

                            string sellerType;
                            string fuelType = null;
                            string engineSize = null;

                            if (tradeTypeOrfuelType != null && tradeTypeOrfuelType.Equals("Trade"))
                            {
                                sellerType = tradeTypeOrfuelType;
                                if (!String.IsNullOrWhiteSpace(fuelTypeOrEngineSize)) fuelType = fuelTypeOrEngineSize;
                                if (!String.IsNullOrWhiteSpace(engineSizeOrNothing)) engineSize = engineSizeOrNothing;
                            }
                            else
                            {
                                sellerType = "Private";
                                if (!String.IsNullOrWhiteSpace(fuelTypeOrEngineSize)) fuelType = tradeTypeOrfuelType;
                                if (!String.IsNullOrWhiteSpace(fuelTypeOrEngineSize)) engineSize = fuelTypeOrEngineSize;
                            }

                            string price = _driver.FindElement(By.XPath($"{path}/a/div[2]/span")).Text.Trim();

                            // Standardise results.
                            try { location = RemoveExcessLocationText.Replace(RemoveLineBreaks.Replace(location, " "), String.Empty); } catch (Exception) { }
                            try { year = RemoveNonNumeric.Replace(year, String.Empty); } catch (Exception) { }
                            try { mileage = RemoveNonNumeric.Replace(mileage, String.Empty); } catch (Exception) { }
                            try { engineSize = RemoveNonNumeric.Replace(engineSize, String.Empty); } catch (Exception) { }
                            try { price = RemoveNonNumeric.Replace(price, String.Empty); } catch (Exception) { }

                            // De-duplication.
                            // First, check if article link exists in db.
                            ArticleVersion dbArticleVersion = null;
                            Article dbArticle = null;
                            bool articleLinkExists = ArticleRepo.Exists(x => x.Link == link);

                            if (articleLinkExists)
                            {
                                // Set existing article version to latest version.
                                dbArticleVersion = ArticleVersionRepo.GetByDesc(x => x.VirtualArticle.Link == link, x => x.VirtualArticle, x => x.Id);

                                // Set existing article.
                                dbArticle = dbArticleVersion.VirtualArticle;

                                // Hash db article version.
                                byte[] dbtitleBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Title);
                                byte[] dblocationBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Location);
                                byte[] dbdescriptionBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Description);
                                byte[] dbYearBytes = { };
                                try { dbYearBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Year.ToString()); } catch (Exception) { }
                                byte[] dbmileageBytes = { };
                                try { dbmileageBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Mileage.ToString()); } catch (Exception) { }
                                byte[] dbSellerTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.SellerType);
                                byte[] dbfuelTypeBytes = { };
                                try { dbfuelTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.FuelType); } catch (Exception) { }
                                byte[] dbengineSizeBytes = { };
                                try { dbengineSizeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.EngineSize.ToString()); } catch (Exception) { }
                                byte[] dbpriceBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Price.ToString());
                                byte[] dbBytes = CombineBytes(dbtitleBytes, dblocationBytes, dbdescriptionBytes, dbYearBytes, dbmileageBytes, dbSellerTypeBytes, dbfuelTypeBytes, dbengineSizeBytes, dbpriceBytes);
                                string dbHash = GenerateHash(dbBytes);

                                // Hash fetched verison of this article.
                                byte[] titleBytes = Encoding.ASCII.GetBytes(title);
                                byte[] locationBytes = Encoding.ASCII.GetBytes(location);
                                byte[] descriptionBytes = Encoding.ASCII.GetBytes(description);
                                byte[] yearBytes = { };
                                try { yearBytes = Encoding.ASCII.GetBytes(year); } catch (Exception) { }
                                byte[] mileageBytes = { };
                                try { mileageBytes = Encoding.ASCII.GetBytes(mileage); } catch (Exception) { }
                                byte[] sellerTypeBytes = Encoding.ASCII.GetBytes(sellerType);
                                byte[] fuelTypeBytes = { };
                                try { fuelTypeBytes = Encoding.ASCII.GetBytes(fuelType); } catch (Exception) { }
                                byte[] engineSizeBytes = { };
                                try { engineSizeBytes = Encoding.ASCII.GetBytes(engineSize); } catch (Exception) { }
                                byte[] priceBytes = Encoding.ASCII.GetBytes(price);
                                byte[] bytes = CombineBytes(titleBytes, locationBytes, descriptionBytes, yearBytes, mileageBytes, sellerTypeBytes, fuelTypeBytes, engineSizeBytes, priceBytes);
                                string hash = GenerateHash(bytes);

                                // Compare hashes, skip saving if they are the same as this means we have the latest version.
                                if (String.Equals(dbHash, hash)) { Log.Info("Skipped duplicate article."); continue; }
                            }

                            // Init vars for db save.
                            Article article = new Article();
                            ArticleVersion articleVersion = new ArticleVersion();
                            string articleState;

                            if (dbArticle == null)
                            {
                                // New article.
                                articleState = "new";
                                article.Link = link;
                                article.CarModelId = _carModelId;
                                ArticleRepo.Create(article);
                                articleVersion.ArticleId = article.Id; // Link new article.
                                articleVersion.Version = 1; // Set first version.
                            }
                            else
                            {
                                // Existing article.
                                articleState = "existing";
                                articleVersion.ArticleId = dbArticle.Id; // Link existing article.
                                articleVersion.Version = dbArticleVersion.Version + 1; // Increment version.
                            }

                            // Set values and save.
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
                            Log.Info($"Saved new article version with {articleState} article.");
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Could not process and save article/article version.", ex.GetBaseException());
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
                Log.Info("Gumtree Scraper finished job.");
                _driver.Quit();
            }
        }

        private static string GetElementXPath(IWebElement element)
        {
            const string javaScript = @"function getElementXPath(elt){var path = """";for (; elt && elt.nodeType == 1; elt = elt.parentNode){idx = getElementIdx(elt);xname = elt.tagName;if (idx > 1)
                                        {xname += ""["" + idx + ""]"";}path = ""/"" + xname + path;}return path;}function getElementIdx(elt){var count = 1;for (var sib = elt.previousSibling; sib;
                                        sib = sib.previousSibling){if(sib.nodeType == 1 && sib.tagName == elt.tagName){count++;}}return count;}return getElementXPath(arguments[0]).toLowerCase();";
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
            byte[] combinedBytes = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, combinedBytes, offset, array.Length);
                offset += array.Length;
            }
            return combinedBytes;
        }

        private static string ToTitleCase(string text)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
        }
    }
}
