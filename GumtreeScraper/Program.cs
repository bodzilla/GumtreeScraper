using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;

namespace GumtreeScraper
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool _failed;

        public static readonly IList<string> ArticleViewList = new List<string>();

        private static void Main()
        {
            try
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
                if (ArticleViewList.Count > 0) new ArticleViewScraper(ArticleViewList);
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
