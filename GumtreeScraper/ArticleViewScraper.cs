using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using GumtreeScraper.Model;
using GumtreeScraper.Repository;
using HtmlAgilityPack;
using log4net;

namespace GumtreeScraper
{
    public class ArticleViewScraper
    {
        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Proxy _proxy = Proxy.Instance;
        private readonly bool _useSleep = bool.Parse(ConfigurationManager.AppSettings["UseSleep"]);
        private readonly int _sleepMin = int.Parse(ConfigurationManager.AppSettings["MinSleepMilliSecs"]);
        private readonly int _sleepMax = int.Parse(ConfigurationManager.AppSettings["MaxSleepMilliSecs"]);

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

                    if (_useSleep)
                    {
                        // Sleep for a bit before making next call to look human.
                        int sleep = new Random().Next(_sleepMin, _sleepMax);
                        _log.Info($"Sleeping for {sleep} ms.");
                        Thread.Sleep(sleep);
                    }

                    _log.Info($"Scraping view: {link}");

                    // Web request response will be read into this variable.
                    string data = null;

                    // Parse response as HTML document.
                    HtmlDocument doc = new HtmlDocument();

                    try
                    {
                        while (data == null)
                        {
                            data = _proxy.MakeRequest(link);
                            doc.LoadHtml(data);

                            // Check if robot detected.
                            var robotTag = doc.DocumentNode.SelectSingleNode("//meta[@name='ROBOTS']");
                            if (robotTag == null) continue;
                            data = null;
                            _proxy.NextProxy();
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Could not get web response for article view.", ex);
                        continue;
                    }

                    try
                    {
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
                        _log.Error($"Could not scrape link: {link}", ex);
                        new ArticleViewScraper(links);
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
