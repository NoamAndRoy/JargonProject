import scrapy

class WebSite(scrapy.Item):
	url = scrapy.Field()
	year = scrapy.Field()

