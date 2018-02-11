using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GumtreeScraper.Model;
using GumtreeScraper.Repository;
using HtmlAgilityPack;
using log4net;

namespace GumtreeScraper
{
    public class ArticleViewScraper
    {
        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly int _timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutSecs"]);

        private readonly ArticleRepository _articleRepo = new ArticleRepository();
        private readonly Regex _removeNonNumeric = new Regex(@"[^\d]");
        private readonly int _failedLinks;

        public ArticleViewScraper(IEnumerable<string> links)
        {
            try
            {
                // Scrape article view list.
                foreach (string link in links)
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(_timeout);

                        try
                        {
                            _log.Info($"Scraping view: {link}");

                            using (Task<HttpResponseMessage> response = client.GetAsync(link))
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
                                            string daysOld = doc.DocumentNode.SelectSingleNode(@"//*[dl[@class=""dl-attribute-list attribute-list1""]]/dl/dd[1]").InnerText.Trim();
                                            if (!String.IsNullOrWhiteSpace(daysOld)) daysOld = _removeNonNumeric.Replace(daysOld, String.Empty);

                                            // Update days old.
                                            Article article = _articleRepo.Get(x => x.Link == link);
                                            article.DaysOld = int.Parse(daysOld);
                                            _articleRepo.Update(article);
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
                            _failedLinks++;
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
                if (_failedLinks > 0) _log.Info($"{_failedLinks} links failed.");
                _log.Info($"Article View Scraper finished scraping.");
            }
        }
    }
}
