using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using GumtreeScraper.Model;
using GumtreeScraper.Repository;
using HtmlAgilityPack;
using log4net;

namespace GumtreeScraper
{
    public class SearchListScraper
    {
        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Proxy _proxy = Proxy.Instance;
        private readonly bool _useSleep = bool.Parse(ConfigurationManager.AppSettings["UseSleep"]);
        private readonly int _sleepMin = int.Parse(ConfigurationManager.AppSettings["MinSleepMilliSecs"]);
        private readonly int _sleepMax = int.Parse(ConfigurationManager.AppSettings["MaxSleepMilliSecs"]);

        private readonly CarMakeRepository _carMakeRepo = new CarMakeRepository();
        private readonly CarModelRepository _carModelRepo = new CarModelRepository();
        private readonly ArticleRepository _articleRepo = new ArticleRepository();
        private readonly ArticleVersionRepository _articleVersionRepo = new ArticleVersionRepository();

        private readonly Regex _removeNonNumeric = new Regex(@"[^\d]");
        private readonly Regex _removeLineBreaks = new Regex(@"\r\n?|\n");
        private readonly Regex _removeExcessLocationText = new Regex(@".* \| ");

        private readonly HashSet<Article> _articleList = new HashSet<Article>();
        private readonly HashSet<string> _articleLinksList = new HashSet<string>();

        private readonly int _failedArticles;

        public SearchListScraper(string p, string u)
        {
            string carMake = String.Empty;
            string carModel = String.Empty;

            try
            {
                // Setting initial variables.
                int pages = int.Parse(p);
                string url = u.ToLower();
                carMake = ToTitleCase(HttpUtility.ParseQueryString(url).Get("vehicle_make"));
                carModel = ToTitleCase(HttpUtility.ParseQueryString(url).Get("vehicle_model"));

                // Check if car make and model exist in db.
                _log.Info($"Asserting existence of {carMake} {carModel} in database..");
                bool carMakeExists = _carMakeRepo.Exists(x => x.Name.Equals(carMake, StringComparison.CurrentCultureIgnoreCase));
                bool carModelExists = _carModelRepo.Exists(x => x.Name.Equals(carModel, StringComparison.CurrentCultureIgnoreCase));

                // Create/get and set ids.
                if (!carMakeExists) _carMakeRepo.Create(new CarMake { Name = carMake });
                if (!carModelExists) _carModelRepo.Create(new CarModel { Name = carModel, CarMakeId = _carMakeRepo.Get(x => x.Name.Equals(carMake)).Id });
                int carModelId = _carModelRepo.Get(x => x.Name.Equals(carModel)).Id;

                // Get all articles and article links.
                _log.Info("Retrieving indexes..");
                _articleList.UnionWith(_articleRepo.GetList(x => x.CarModelId == carModelId && x.Active, x => x.VirtualArticleVersions));
                _articleLinksList.UnionWith(_articleRepo.GetList(x => x.CarModelId == carModelId && x.Active).Select(x => x.Link));

                // Scrape search list by paging through from oldest to latest page.
                for (int i = pages; i >= 1; i--)
                {
                    try
                    {
                        if (_useSleep)
                        {
                            // Sleep for a bit before making next call to look human.
                            int sleep = new Random().Next(_sleepMin, _sleepMax);
                            _log.Info($"Sleeping for {sleep} ms.");
                            Thread.Sleep(sleep);
                        }

                        // Set page.
                        string currentPage = $"{url}&page={i}";
                        _log.Info($"Scraping page: {currentPage}");

                        // Data notes.
                        string data = null;
                        HtmlNodeCollection results = null;

                        try
                        {
                            // Ensure results are populated after web request.
                            while (results == null)
                            {
                                data = _proxy.MakeRequest(currentPage);

                                // Parse response as HTML document.
                                HtmlDocument doc = new HtmlDocument();
                                doc.LoadHtml(data);
                                results = doc.DocumentNode.SelectNodes(@"//*[a[@class=""listing-link""]]");

                                // If results empty, use the next proxy and retry.
                                if (results == null) _proxy.NextProxy();
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Could not get web response for url: {currentPage}", ex);
                            continue;
                        }

                        // If no response, skip to next.
                        if (String.IsNullOrEmpty(data))
                        {
                            _log.Error("Skipping scrape page due to null content.");
                            continue;
                        }

                        foreach (HtmlNode result in results.Reverse())
                        {
                            string path = result.XPath;

                            try
                            {
                                // Get article values.
                                string link = result.SelectSingleNode($"{path}/a").GetAttributeValue("href", null);

                                // If link or title are empty, this is not a valid article.
                                if (String.IsNullOrWhiteSpace(link)) continue;

                                // Add domain name to link value.
                                link = link.Insert(0, "https://www.gumtree.com");

                                string title = result.SelectSingleNode($"{path}/a/div[2]/h2").InnerText.Trim();
                                string location = result.SelectSingleNode($"{path}/a/div[2]/div[1]/span").InnerText.Trim();
                                string description = result.SelectSingleNode($"{path}/a/div[2]/p").InnerText.Trim();

                                // Thumbnail link could be in either of these tags.
                                string thumbnail = null;

                                try
                                {
                                    if (!String.IsNullOrEmpty(result.SelectSingleNode($"{path}/a/div[1]/div/img").GetAttributeValue("src", null)))
                                        thumbnail = result.SelectSingleNode($"{path}/a/div[1]/div/img").GetAttributeValue("src", null);
                                    else if (!String.IsNullOrEmpty(result.SelectSingleNode($"{path}/a/div[1]/div/img").GetAttributeValue("data-lazy", null)))
                                        thumbnail = result.SelectSingleNode($"{path}/a/div[1]/div/img").GetAttributeValue("data-lazy", null);
                                }
                                catch (Exception)
                                {
                                    if (!String.IsNullOrWhiteSpace(result.SelectSingleNode($"{path}/a/div[1]/div[1]/div/img").GetAttributeValue("src", null)))
                                        thumbnail = result.SelectSingleNode($"{path}/a/div[1]/div[1]/div/img").GetAttributeValue("src", null);
                                    else if (!String.IsNullOrWhiteSpace(result.SelectSingleNode($"{path}/a/div[1]/div[1]/div/img").GetAttributeValue("data-lazy", null)))
                                        thumbnail = result.SelectSingleNode($"{path}/a/div[1]/div[1]/div/img").GetAttributeValue("data-lazy", null);
                                }

                                // Loop through property list as some may not exist.
                                string year = null;
                                string mileage = null;
                                string sellerType = null;
                                string fuelType = null;
                                string engineSize = null;
                                string updates = null;

                                HtmlNodeCollection details = result.SelectNodes($"{path}/a/div[2]/ul/li");

                                foreach (HtmlNode detail in details)
                                {
                                    HtmlNode span = detail.SelectSingleNode($"{detail.XPath}/span[2]");
                                    string spanText = span.InnerText.Trim();
                                    string property = span.GetAttributeValue("itemprop", String.Empty);

                                    switch (property)
                                    {
                                        case "dateVehicleFirstRegistered":
                                            year = spanText;
                                            break;

                                        case "mileageFromOdometer":
                                            mileage = spanText;
                                            break;

                                        case "": // Seller field appears as empty.
                                            sellerType = spanText.Equals("Trade") ? "Trade" : "Private";
                                            break;

                                        case "fuelType":
                                            fuelType = spanText;
                                            break;

                                        case "vehicleEngine":
                                            engineSize = spanText;
                                            break;
                                    }
                                }

                                sellerType = String.IsNullOrEmpty(sellerType) ? "Private" : "Trade";

                                string daysOld = null;
                                try
                                {
                                    daysOld = result.SelectSingleNode($"{path}/a/div[2]/div[2]/span").InnerText.Trim();
                                }
                                catch (Exception)
                                {
                                    // ignored
                                }

                                string price = result.SelectSingleNode($"{path}/a/div[2]/span").InnerText.Trim();

                                // Standardise posted value.
                                if (String.IsNullOrEmpty(daysOld))
                                {
                                    // if it's empty, add to view list so we can fetch the days old later.
                                    if (!_articleLinksList.Contains(link) && !Program.ArticleViewStack.Contains(link)) Program.ArticleViewStack.Push(link);
                                }
                                else if (!daysOld.Contains("days") && !daysOld.Contains("day"))
                                {
                                    // If it's not empty doesn't contain these days, it must mean it's from today.
                                    daysOld = "0";
                                }

                                // Cleanse results.
                                if (!String.IsNullOrWhiteSpace(thumbnail)) thumbnail = thumbnail.Replace("99.JPG", "93.JPG"); // Larger version of thumbnail.
                                if (!String.IsNullOrWhiteSpace(location)) location = _removeExcessLocationText.Replace(_removeLineBreaks.Replace(location, " "), String.Empty);
                                if (!String.IsNullOrWhiteSpace(year)) year = _removeNonNumeric.Replace(year, String.Empty);
                                mileage = !String.IsNullOrWhiteSpace(mileage) ? _removeNonNumeric.Replace(mileage, String.Empty) : null;
                                if (!String.IsNullOrWhiteSpace(engineSize)) engineSize = _removeNonNumeric.Replace(engineSize, String.Empty);
                                if (!String.IsNullOrWhiteSpace(daysOld)) daysOld = _removeNonNumeric.Replace(daysOld, String.Empty);
                                price = _removeNonNumeric.Replace(price, String.Empty);

                                // De-duplication.
                                // First, check if article link exists in db.
                                ArticleVersion dbArticleVersion = null;
                                Article dbArticle = null;
                                bool articleLinkExists = _articleLinksList.Contains(link);

                                if (articleLinkExists)
                                {
                                    try
                                    {
                                        // Set existing article and latest article version.
                                        dbArticle = _articleList.Single(x => x.Link == link);
                                        dbArticleVersion = dbArticle.VirtualArticleVersions.OrderByDescending(x => x.Version).First();
                                    }
                                    catch (Exception ex)
                                    {
                                        _log.Error($"Could not get dbArticle/dbArticleVersion, removing from db. Link: {dbArticle.Link}", ex);
                                        if (dbArticleVersion != null) { _articleVersionRepo.Delete(dbArticleVersion); }
                                        if (dbArticle != null) _articleRepo.Delete(dbArticle);
                                        if (_articleList.Contains(dbArticle)) _articleList.Remove(dbArticle);
                                        if (_articleLinksList.Contains(link)) _articleLinksList.Remove(link);
                                        continue;
                                    }

                                    // Hash db article version.
                                    byte[] dbTitleBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Title);
                                    byte[] dbDescriptionBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Description);
                                    byte[] dbYearBytes = { };
                                    if (!String.IsNullOrWhiteSpace(dbArticleVersion.Year.ToString())) dbYearBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Year.ToString());
                                    byte[] dbMileageBytes = { };
                                    if (!String.IsNullOrWhiteSpace(dbArticleVersion.Mileage.ToString())) dbMileageBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Mileage.ToString());
                                    byte[] dbSellerTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.SellerType);
                                    byte[] dbFuelTypeBytes = { };
                                    if (!String.IsNullOrWhiteSpace(dbArticleVersion.FuelType)) dbFuelTypeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.FuelType);
                                    byte[] dbEngineSizeBytes = { };
                                    if (!String.IsNullOrWhiteSpace(dbArticleVersion.EngineSize.ToString())) dbEngineSizeBytes = Encoding.ASCII.GetBytes(dbArticleVersion.EngineSize.ToString());
                                    byte[] dbPriceBytes = Encoding.ASCII.GetBytes(dbArticleVersion.Price.ToString());
                                    byte[] dbBytes = CombineBytes(dbTitleBytes, dbDescriptionBytes, dbYearBytes, dbMileageBytes, dbSellerTypeBytes, dbFuelTypeBytes, dbEngineSizeBytes, dbPriceBytes);
                                    string dbHash = GenerateHash(dbBytes);

                                    // Hash fetched verison of this article.
                                    byte[] titleBytes = Encoding.ASCII.GetBytes(title);
                                    byte[] descriptionBytes = Encoding.ASCII.GetBytes(description);
                                    byte[] yearBytes = { };
                                    if (!String.IsNullOrWhiteSpace(year)) yearBytes = Encoding.ASCII.GetBytes(year);
                                    byte[] mileageBytes = { };
                                    if (!String.IsNullOrWhiteSpace(mileage)) mileageBytes = Encoding.ASCII.GetBytes(mileage);
                                    byte[] sellerTypeBytes = Encoding.ASCII.GetBytes(sellerType);
                                    byte[] fuelTypeBytes = { };
                                    if (!String.IsNullOrWhiteSpace(fuelType)) fuelTypeBytes = Encoding.ASCII.GetBytes(fuelType);
                                    byte[] engineSizeBytes = { };
                                    if (!String.IsNullOrWhiteSpace(engineSize)) engineSizeBytes = Encoding.ASCII.GetBytes(engineSize);
                                    byte[] priceBytes = Encoding.ASCII.GetBytes(price);
                                    byte[] bytes = CombineBytes(titleBytes, descriptionBytes, yearBytes, mileageBytes, sellerTypeBytes, fuelTypeBytes, engineSizeBytes, priceBytes);
                                    string hash = GenerateHash(bytes);

                                    // Compare hashes, skip saving if they are the same as this means we have the latest version.
                                    if (String.Equals(dbHash, hash))
                                    {
                                        // Update thumbnail.
                                        if (!String.Equals(dbArticle.Thumbnail, thumbnail))
                                        {
                                            dbArticle.Thumbnail = thumbnail;
                                            _articleRepo.Update(dbArticle);
                                        }

                                        _log.Info("Skipped duplicate article.");
                                        continue;
                                    }

                                    // Check if price changed.
                                    if (int.Parse(price) > dbArticleVersion.Price) updates += $"Price increased from £{dbArticleVersion.Price:N0}. ";
                                    if (int.Parse(price) < dbArticleVersion.Price) updates += $"Price decreased from £{dbArticleVersion.Price:N0}. ";

                                    // Check if mileage changed.
                                    if (mileage != null && dbArticleVersion.Mileage == null) updates += "Mileage newly added. ";
                                    if (mileage != null && int.Parse(mileage) > dbArticleVersion.Mileage) updates += $"Mileage increased from {dbArticleVersion.Mileage:N0}. ";
                                    if (mileage != null && int.Parse(mileage) < dbArticleVersion.Mileage) updates += $"Mileage decreased from {dbArticleVersion.Mileage:N0}. ";

                                    // Check if location changed.
                                    if (location != null && !String.Equals(dbArticleVersion.Location, location)) updates += $"Location updated from {dbArticleVersion.Location}.";

                                    // Check if thumbnail changed.
                                    if (thumbnail != null && !String.Equals(dbArticle.Thumbnail, thumbnail)) updates += "Thumbnail updated.";
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
                                    article.Thumbnail = thumbnail;
                                    article.CarModelId = carModelId;
                                    article.DaysOld = daysOld != null ? int.Parse(daysOld) : (int?)null;
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
                                articleVersion.Mileage = !String.IsNullOrWhiteSpace(mileage) ? int.Parse(mileage) : (int?)null;
                                articleVersion.SellerType = sellerType;
                                articleVersion.FuelType = fuelType;
                                articleVersion.EngineSize = engineSize != null ? int.Parse(engineSize) : (int?)null;
                                articleVersion.Price = int.Parse(price);
                                articleVersion.Updates = updates;
                                _articleVersionRepo.Create(articleVersion);

                                // Add to hash sets.
                                if (dbArticle == null)
                                {
                                    article.VirtualArticleVersions.Add(articleVersion);
                                    _articleList.Add(article);
                                    _articleLinksList.Add(link);
                                }
                                else
                                {
                                    _articleList.Remove(dbArticle);
                                    dbArticle.VirtualArticleVersions.Add(articleVersion);
                                    _articleList.Add(dbArticle);
                                }

                                _log.Info($"Saved new article version with {articleState} article.");
                            }
                            catch (Exception ex)
                            {
                                _log.Error("Could not process and save article/article version.", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _failedArticles++;
                        _log.Error($"Could not get/process web response for {carMake} {carModel}.", ex);
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            finally
            {
                if (_failedArticles > 0) _log.Info($"{_failedArticles} articles failed.");
                _log.Info($"Search List Scraper finished scraping for {carMake} {carModel}.");
            }
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
