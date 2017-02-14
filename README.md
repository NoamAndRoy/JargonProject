#JargonProject

The De-Jargonizer is an automated jargon identification program aimed at helping scientists and science communication trainers improve and adapt vocabulary use for a variety of audiences. The program determines the level of vocabulary and terms in a text, and divides the words into three levels: high frequency/common words; mid-frequency/normal words; and jargon – rare and technical words.


##Crawler & GeneralSpider
Over 90 million words were counted using a crawler that counted all words in all ~250,000 articles published in the BBC sites (including science related channels) during the years 2012-2015. These articles were crawled using scrapy framework (http://scrapy.org/). The crawling included only articles, and ignored advertisements, reader comments, phone numbers, websites, and emails. All words were extracted to an excel sheet, creating a dictionary of every new word found, and the number of appearances in the corpus. 

Overall, ~500,000 word types were ordered by number of appearances. These word types refer to each word: for example, value and values are each unique word types, even though they belong to the same word family.

* The Crawler saves lists of articles published from the BBC sites (including science related channels) during the years 2012-2015.
* The GeneralSpider crawl over the urls that were saved by the Crawler and created a dictionary of every word found, and the number of appearances in the corpus.

To use the spiders, create a new one using Scrapy, and replace the files with the corresponding files from the repository.



##Website
The website contains the full ASP.NET website implementation of the De-Jargonizer.

To use the websie:

1. Open the project with visual studio.
2. Compile and run it.

##Technologies

* [Scrapy](https://scrapy.org/) - The crawler used the scrapy framework.
* [Docx](https://docx.codeplex.com/) - The website is using it to read Docx files.
* [CSV Reader](https://www.codeproject.com/Articles/9258/A-Fast-CSV-Reader) - The website is using it to read the matrix file.
