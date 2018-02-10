# GumtreeScraper

**Runtime params:**
Not required anymore. See ScrapeList in app.config.

**ScrapeList:**
Multiple scrape lists can now be added in the latest version. You can add these settings in the app.config file, just make sure the key contains "Scrape". Some examples can be found in the app.config where the first parameter is the number of pages to scrape on that particular vehicle and the second parameter is the search URL.

**Gumtree URLs:**
Gumtree can be weird when paging through a result set. For some reason, it doesn't like particular links, so when you try paging through, the same articles will load regardless of the page you're on. As well as this, Gumtree doesn't seem to like links with encoded HTML entities either, so if your URL isn't "correctly formatted", you may end up with some very weird scrape results on this also.

As a result, I've found a link combination that'll always (hopefully) work, modify to your criteria:

> https://www.gumtree.com/search?search_category=cars&search_location=yourpostcode&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&photos_filter=true&vehicle_mileage=up_to_80000

**Querying the database:**
If you copy a result link from your web browswer in order to do a search for it in your database, keep in mind that some browsers URL parsers encode/decode HTML entities, which may affect searching on article links if they don't match up properly.

**Database structure**:
![database](https://github.com/bodzilla/GumtreeScraper/blob/master/GumtreeScraper.DataAccess/DatabaseModel.png)
