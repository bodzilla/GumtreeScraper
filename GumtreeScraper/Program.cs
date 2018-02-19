using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using GumtreeScraper.Repository;
using log4net;

namespace GumtreeScraper
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool _failed;

        public static readonly Stack<string> ArticleViewStack = new Stack<string>();

        private static void Main(string[] args)
        {
            try
            {
                // Update days old for all existing articles and deletes non-existing articles.
                if (args.Length > 0)
                {
                    Log.Info("Starting Back Burner Mode..");

                    // Only get active & non-archived articles to reduce time and bandwidth usage of Article scraper.
                    foreach (string link in new ArticleRepository().GetAll().Where(x => x.Active && x.Archived == false).OrderBy(x => x.DaysOld == null).ThenBy(x => x.Id).Select(x => x.Link)) ArticleViewStack.Push(link);
                    new ArticleViewScraper(ArticleViewStack);
                }
                else
                {
                    Log.Info("Retrieving runtime variables..");

                    // Get ScrapeList.
                    string[][] scrapeList = ConfigurationManager.AppSettings.AllKeys
                        .Where(key => key.Contains("Scrape"))
                        .Select(key => ConfigurationManager.AppSettings[key].Split(' '))
                        .ToArray();

                    // Run for all search lists.
                    Log.Info("Starting Search List Scraper..");
                    foreach (string[] list in scrapeList)
                    {
                        new SearchListScraper(list[0], list[1]);
                    }

                    // Run for all article view links.
                    Log.Info("Starting Article View Scraper..");
                    new ArticleViewScraper(ArticleViewStack);
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Fatal("Could not run GumtreeScraper.", ex.GetBaseException());
            }
            finally
            {
                Log.Info("Scraping complete. Exiting GumtreeScraper..");
                Thread.Sleep(10000);
                if (_failed) Environment.Exit(1);
                Environment.Exit(0);
            }
        }
    }
}
