# GumtreeScraper

**Runtime params:**
Not required anymore. See ScrapeList in app.config.

**App config - ScrapeList:**
Multiple scrape lists can now be added in the latest version. You can add these settings in the app.config file, just make sure the key contains "Scrape". Some examples can be found in the app.config, where the first parameter is the number of pages to scrape on that particular vehicle, and the second parameter is the search URL.

*Note: if your URL isn't "valid" - you might get very weird scrape results..*

**Gumtree URLs:**
Gumtree can be weird when paging through a result set. For some reason, it doesn't like particular links - so when you try paging through, the same page always loads. As well as this, Gumtree doesn't seem to link links with broken HTML entities, weird.

As a result, I've found a link combination that'll always (hopefully) work, modify to your criteria:

> https://www.gumtree.com/search?search_category=cars&search_location=yourpostcode&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&photos_filter=true&vehicle_mileage=up_to_80000

**Querying the database:**
If you copy a result link from your web browswer in order to do a search for it in your database, keep in mind that some browser copy&paste do not "break" html entities.

**Database structure with dependencies**:
![db](https://github.com/bodzilla/GumtreeScraper/blob/master/GumtreeScraper.DataAccess/DatabaseModel.png)
