using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using log4net;

namespace GumtreeScraper
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static void Main()
        {
            Log.Info("Retrieving runtime variables..");

            // Get ScrapeList.
            string[][] scrapeList = ConfigurationManager.AppSettings.AllKeys
                .Where(key => key.Contains("Scrape"))
                .Select(key => ConfigurationManager.AppSettings[key].Split(' '))
                .ToArray();

            // Run GumreeScraper for all lists.
            foreach (string[] list in scrapeList)
            {
                new GumtreeScraper(list[0], list[1]);
            }
            Log.Info("All lists have been scraped successfully, finished GumtreeScraper session.");
        }
    }
}
