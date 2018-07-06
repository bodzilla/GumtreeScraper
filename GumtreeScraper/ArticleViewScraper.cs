using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using GumtreeScraper.Model;
using GumtreeScraper.Repository;
using HtmlAgilityPack;
using log4net;

namespace GumtreeScraper
{
    public class ArticleViewScraper
    {
        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly int _timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
        private readonly ArticleRepository _articleRepo = new ArticleRepository();
        private readonly Regex _removeNonNumeric = new Regex(@"[^\d]");

        public ArticleViewScraper(Stack<string> links)
        {
            try
            {
                // Scrape article view stack.
                while (links.Count > 0)
                {
                    string link = links.Pop();

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(link);
                    request.Timeout = _timeout;

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        using (Stream stream = response.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                try
                                {
                                    _log.Info($"Scraping view: {link}");

                                    // Parse response as HTML document.
                                    string data = reader.ReadToEnd();
                                    HtmlDocument doc = new HtmlDocument();
                                    doc.LoadHtml(data);
                                    string daysOld;

                                    // Days old field may be in two different locations, depending on the UI version.
                                    try
                                    {
                                        daysOld = doc.DocumentNode.SelectSingleNode(@"//*[dl[@class=""attributes-group attributes-entry""]]/dl[2]").InnerText.Trim();
                                    }
                                    catch (Exception)
                                    {
                                        try
                                        {
                                            // Check if removed label exists first.
                                            string removedLbl = doc.DocumentNode.SelectSingleNode(@"//*[div[@class=""media-body""]]/div[2]").InnerText.Trim();
                                            if (!String.IsNullOrWhiteSpace(removedLbl)) throw new Exception();
                                            daysOld = doc.DocumentNode.SelectSingleNode(@"//*[dl[@class=""dl-attribute-list attribute-list1""]]/dl/dd[1]").InnerText.Trim();
                                        }
                                        catch (Exception)
                                        {
                                            Article inactiveArticle = _articleRepo.Get(x => x.Link == link);
                                            inactiveArticle.Active = false;
                                            _articleRepo.Update(inactiveArticle);
                                            _log.Info("Setting article as inactive.");
                                            continue;
                                        }
                                    }

                                    if (!daysOld.Contains("days") && !daysOld.Contains("day"))
                                    {
                                        // If it's not empty doesn't contain these days, it must mean it's from today.
                                        daysOld = "0";
                                    }

                                    if (!String.IsNullOrWhiteSpace(daysOld)) daysOld = _removeNonNumeric.Replace(daysOld, String.Empty);

                                    // Update days old.
                                    Article article = _articleRepo.Get(x => x.Link == link);

                                    if (!String.Equals(article.DaysOld.ToString(), daysOld))
                                    {
                                        article.DaysOld = int.Parse(daysOld);
                                        _articleRepo.Update(article);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _log.Error("Could not get/process article view fields.", ex);
                                    new ArticleViewScraper(links);
                                }
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _log.Error("Could not get/process web response.", ex);
                new ArticleViewScraper(links);
            }
            finally
            {
                _log.Info("Article View Scraper finished scraping.");
            }
        }
    }
}
