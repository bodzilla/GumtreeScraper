﻿using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using GumtreeScraper.Model;
using GumtreeScraper.Repository;
using HtmlAgilityPack;
using log4net;

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

        private int _failedArticles;

        public GumtreeScraper(string p, string u)
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

                _log.Info($"Asserting existence of {carMake} {carModel} in database..");

                // Check if car make and model exist in db.
                bool carMakeExists =
                    _carMakeRepo.Exists(x => x.Name.Equals(carMake, StringComparison.CurrentCultureIgnoreCase));
                bool carModelExists =
                    _carModelRepo.Exists(x => x.Name.Equals(carModel, StringComparison.CurrentCultureIgnoreCase));

                // Create/get and set ids.
                if (!carMakeExists) _carMakeRepo.Create(new CarMake { Name = carMake });
                if (!carModelExists) _carModelRepo.Create(new CarModel { Name = carModel, CarMakeId = _carMakeRepo.Get(x => x.Name.Equals(carMake)).Id });
                int carModelId = _carModelRepo.Get(x => x.Name.Equals(carModel)).Id;

                // Scrape results by paging through from oldest to latest page.
                for (int i = pages; i >= 1; i--)
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(_timeout);

                        try
                        {
                            // Set page.
                            string currentPage = $"{url}&page={i}";
                            _log.Info($"Scraping page: {currentPage}");

                            using (Task<HttpResponseMessage> response = client.GetAsync(currentPage))
                            {
                                using (Task<Stream> stream = response.Result.Content.ReadAsStreamAsync())
                                {
                                    if (stream.Result != null)
                                    {
                                        using (StreamReader reader = new StreamReader(stream.Result, Encoding.UTF8))
                                        {
                                            // Parse response as HTML document.
                                            string data = reader.ReadToEnd();
                                            HtmlDocument doc = new HtmlDocument();
                                            doc.LoadHtml(data);
                                            HtmlNodeCollection results = doc.DocumentNode.SelectNodes(@"//*[a[@class=""listing-link""]]");

                                            foreach (HtmlNode result in results)
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

                                                    // Loop through property list as some may not exist.
                                                    string year = null;
                                                    string mileage = null;
                                                    string sellerType = null;
                                                    string fuelType = null;
                                                    string engineSize = null;

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

                                                    string daysOld = String.Empty;
                                                    try { daysOld = result.SelectSingleNode($"{path}/a/div[2]/div[2]/span").InnerText.Trim(); } catch (Exception) { }

                                                    string price = result.SelectSingleNode($"{path}/a/div[2]/span").InnerText.Trim();

                                                    // Standardise posted value.
                                                    if (!daysOld.Contains("days") || !daysOld.Contains("day") ||
                                                        daysOld.Equals("URGENT", StringComparison.CurrentCultureIgnoreCase) || String.IsNullOrEmpty(daysOld))
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

                                                        try
                                                        {
                                                            // Set existing article.
                                                            dbArticle = dbArticleVersion.VirtualArticle;
                                                        }
                                                        catch (Exception)
                                                        {
                                                            _log.Error("Could not get dbArticle.");
                                                            continue;
                                                        }

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
                                    else
                                    {
                                        throw new NullReferenceException("Web request returns null, check request.");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _failedArticles++;
                            _log.Error("Could not get web response.", ex.InnerException);
                            throw;
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
                if (_failedArticles > 0) _log.Info($"{_failedArticles} articles failed.");
                _log.Info($"Gumtree Scraper finished scraping for {carMake} {carModel}.");
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
