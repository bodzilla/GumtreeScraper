using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GumtreeScraper.Model;
using GumtreeScraper.Repository;
using log4net;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.UI;

namespace GumtreeScraper
{
    public class GumtreeScraper
    {
        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly int _timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutSecs"]);

        private readonly CarMakeRepository _carMakeRepo = new CarMakeRepository();
        private readonly CarModelRepository _carModelRepo = new CarModelRepository();
        private readonly ArticleRepository _articleRepo = new ArticleRepository();
        private readonly ArticleVersionRepository _articleVersionRepo = new ArticleVersionRepository();

        private readonly Regex _removeNonNumeric = new Regex(@"[^\d]");
        private readonly Regex _removeLineBreaks = new Regex(@"\r\n?|\n");
        private readonly Regex _removeExcessLocationText = new Regex(@".* \| ");

        private readonly IWebDriver _driver;

        public GumtreeScraper(IReadOnlyList<string> args)
        {
            string carMake = String.Empty;
            string carModel = String.Empty;

            try
            {
                // Setting initial variables.
                carMake = ToTitleCase(args[0]);
                carModel = ToTitleCase(args[1]);
                int pages = int.Parse(args[2]);
                string url = args[3];

                _log.Info($"Asserting existence of {carMake} {carModel} in database..");

                // Check if car make and model exist in db.
                bool carMakeExists = _carMakeRepo.Exists(x => x.Name.Equals(carMake, StringComparison.CurrentCultureIgnoreCase));
                bool carModelExists = _carModelRepo.Exists(x => x.Name.Equals(carModel, StringComparison.CurrentCultureIgnoreCase));

                // Create/get and set ids.
                if (!carMakeExists) _carMakeRepo.Create(new CarMake { Name = carMake });
                if (!carModelExists) _carModelRepo.Create(new CarModel { Name = carModel, CarMakeId = _carMakeRepo.Get(x => x.Name.Equals(carMake)).Id });
                int carModelId = _carModelRepo.Get(x => x.Name.Equals(carModel)).Id;

                // Set up driver.
                _log.Info("Initialising Gumtree Scraper..");
                PhantomJSDriverService service = PhantomJSDriverService.CreateDefaultService();
                service.AddArgument("--webdriver-loglevel=NONE");
                _driver = new PhantomJSDriver(service);
                _driver.Manage().Window.Maximize(); // To capture all content.

                WebDriverWait wait = new WebDriverWait(_driver, new TimeSpan(0, 0, _timeout));

                // Scrape results by paging through from oldest to latest page.
                for (int i = pages; i >= 1; i--)
                {
                    // Set page.
                    string currentPage = $"{url}&page={i}";
                    _log.Info($"Scraping page: {currentPage}");
                    _driver.Url = currentPage;
                    wait.Until(d => d.FindElement(By.XPath(@"//*[@id=""srp-results""]/div[1]"))); // Results div.

                    // Find article results from oldest to newest article.
                    IList<IWebElement> results = _driver.FindElements(By.XPath(@"//*[a[@class=""listing-link""]]"));
                    foreach (IWebElement result in results.Reverse())
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
                            try { year = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[1]/span[2]")).Text.Trim(); } catch (Exception) { _log.Debug("Could not get year."); }

                            string mileage = null;
                            try { mileage = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[2]/span[2]")).Text.Trim(); } catch (Exception) { _log.Debug("Could not get mileage."); }

                            string tradeTypeOrfuelType = null;
                            try { tradeTypeOrfuelType = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[3]/span[2]")).Text.Trim(); } catch (Exception) { _log.Debug("Could not get tradeTypeOrfuelType."); }

                            string fuelTypeOrEngineSize = null;
                            try { fuelTypeOrEngineSize = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[4]/span[2]")).Text.Trim(); } catch (Exception) { _log.Debug("Could not get fuelTypeOrEngineSize."); }

                            string engineSizeOrNothing = null;
                            try { engineSizeOrNothing = _driver.FindElement(By.XPath($"{path}/a/div[2]/ul/li[5]/span[2]")).Text.Trim(); } catch (Exception) { _log.Debug("Could not get engineSizeOrNothing."); }

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

                            string daysOld = null;
                            try { daysOld = _driver.FindElement(By.XPath($"{path}/a/div[2]/div[2]/span")).Text.Trim(); } catch (Exception) { _log.Debug("Could not get postedOn."); }

                            string price = _driver.FindElement(By.XPath($"{path}/a/div[2]/span")).Text.Trim();

                            // Standardise posted value.
                            if (!daysOld.Contains("days") || !daysOld.Contains("day") || daysOld.Equals("URGENT", StringComparison.CurrentCultureIgnoreCase))
                            {
                                daysOld = "0";
                            }

                            // Cleanse results.
                            try { location = _removeExcessLocationText.Replace(_removeLineBreaks.Replace(location, " "), String.Empty); } catch (Exception) { }
                            try { year = _removeNonNumeric.Replace(year, String.Empty); } catch (Exception) { }
                            try { mileage = _removeNonNumeric.Replace(mileage, String.Empty); } catch (Exception) { }
                            try { engineSize = _removeNonNumeric.Replace(engineSize, String.Empty); } catch (Exception) { }
                            try { daysOld = _removeNonNumeric.Replace(daysOld, String.Empty); } catch (Exception) { }
                            try { price = _removeNonNumeric.Replace(price, String.Empty); } catch (Exception) { }

                            // De-duplication.
                            // First, check if article link exists in db.
                            ArticleVersion dbArticleVersion = null;
                            Article dbArticle = null;
                            bool articleLinkExists = _articleRepo.Exists(x => x.Link == link);

                            if (articleLinkExists)
                            {
                                // Set existing article version to latest version.
                                dbArticleVersion = _articleVersionRepo.GetByDesc(x => x.VirtualArticle.Link == link, x => x.VirtualArticle, x => x.Id);

                                // Set existing article.
                                dbArticle = dbArticleVersion.VirtualArticle;

                                // Hash db article version.
                                byte[] dbTitleBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Title);
                                byte[] dbLocationBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Location);
                                byte[] dbDescriptionBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Description);
                                byte[] dbYearBytes = { };
                                try { dbYearBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Year.ToString()); } catch (Exception) { }
                                byte[] dbMileageBytes = { };
                                try { dbMileageBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Mileage.ToString()); } catch (Exception) { }
                                byte[] dbSellerTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.SellerType);
                                byte[] dbFuelTypeBytes = { };
                                try { dbFuelTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.FuelType); } catch (Exception) { }
                                byte[] dbEngineSizeBytes = { };
                                try { dbEngineSizeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.EngineSize.ToString()); } catch (Exception) { }
                                byte[] dbPriceBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Price.ToString());
                                byte[] dbBytes = CombineBytes(dbTitleBytes, dbLocationBytes, dbDescriptionBytes, dbYearBytes, dbMileageBytes, dbSellerTypeBytes, dbFuelTypeBytes, dbEngineSizeBytes, dbPriceBytes);
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
                                if (String.Equals(dbHash, hash))
                                {
                                    // Update the posted time.
                                    if (!String.Equals(dbArticleVersion.DaysOld.ToString(), daysOld))
                                    {
                                        dbArticleVersion.DaysOld = int.Parse(daysOld);
                                        _articleVersionRepo.Update(dbArticleVersion);
                                    }

                                    _log.Info("Skipped duplicate article.");
                                    continue;
                                }
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
                                article.CarModelId = carModelId;
                                _articleRepo.Create(article);
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
                            articleVersion.DaysOld = int.Parse(daysOld);
                            articleVersion.Price = int.Parse(price);
                            _articleVersionRepo.Create(articleVersion);
                            _log.Info($"Saved new article version with {articleState} article.");
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Could not process and save article/article version.", ex.GetBaseException());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Fatal(ex.GetBaseException());
            }
            finally
            {
                _driver.Quit();
                _log.Info($"Gumtree Scraper finished scraping for {carMake} {carModel}.");
            }
        }

        private string GetElementXPath(IWebElement element)
        {
            const string javaScript = @"function getElementXPath(elt){var path = """";for (; elt && elt.nodeType == 1; elt = elt.parentNode){idx = getElementIdx(elt);xname = elt.tagName;if (idx > 1)
                                        {xname += ""["" + idx + ""]"";}path = ""/"" + xname + path;}return path;}function getElementIdx(elt){var count = 1;for (var sib = elt.previousSibling; sib;
                                        sib = sib.previousSibling){if(sib.nodeType == 1 && sib.tagName == elt.tagName){count++;}}return count;}return getElementXPath(arguments[0]).toLowerCase();";
            return (string)((IJavaScriptExecutor)_driver).ExecuteScript(javaScript, element);
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
