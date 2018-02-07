# GumtreeScraper
**Runtime params:**
> .\GumtreeScraper.exe [CarMake] [CarModel] [GumtreeSearchList]

**GumtreeSearchLists:**
Gumtree can be weird when paging through a result set. For some reason, it doesn't like particular links - so when you try paging through, the same page always loads. As a result, I've found a link combination that'll always (hopefully) work, modify to your criteria:

- > https://www.gumtree.com/search?search_category=cars&search_location=yourpostcode&vehicle_make=renault&vehicle_model=clio&distance=50&max_price=2000&min_price=500&photos_filter=true&vehicle_mileage=up_to_80000

**Querying the database:**
- If you copy a result link from your web browswer in order to do a search for it in your database, keep in mind that some browser copy&paste do not "break" html entities.

**Database structure with dependencies**:
![db](https://github.com/bodzilla/GumtreeScraper/blob/master/GumtreeScraper/DatabaseModel.png)
