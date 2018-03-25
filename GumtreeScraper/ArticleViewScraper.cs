﻿using System;
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

        public ArticleViewScraper(Stack<string> links)
        {
            try
            {
                // Scrape article view stack.
                while (links.Count > 0)
                {
                    string link = links.Pop();

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
                                    }
                                    else
                                    {
                                        throw new NullReferenceException($"Web request returns null, check link: {link}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            client.Dispose();
                            _log.Error("Could not get/process web response.", ex.GetBaseException());
                            new ArticleViewScraper(links);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.GetBaseException());
            }
            finally
            {
                _log.Info("Article View Scraper finished scraping.");
            }
        }
    }
}
