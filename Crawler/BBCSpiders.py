import os, json
import datetime
from scrapy.spiders import CrawlSpider, Rule
from scrapy.linkextractors import LinkExtractor
from scrapy.signalmanager import SignalManager
from scrapy import signals
from pydispatch import dispatcher
from items import WebSite

class BBCSpiders(CrawlSpider):
	name = "BBCSpiders"
	allowed_domains = ["bbc.com"]
	scienceCategories = []
	
	visitedUrls = {}
	savedToday = False

	
	def __init__(self, *args, **kwargs):
		super(BBCSpiders, self).__init__(*args, **kwargs)
		SignalManager(dispatcher.Any).connect(self.spiderClosed, signals.spider_closed)
				
	def getScienceCategories():
		cat = []
		with open(os.path.join(os.path.dirname(__file__), 'scienceCategories.txt'), 'r') as scienceFile:
			for line in scienceFile:
				cat.append(line[:-1])
			return cat
	
	scienceCategories = getScienceCategories()
	
	def getSeeds():
		seeds = []
		
		with open(os.path.join(os.path.dirname(__file__), 'seed.txt'), 'r') as seedFile:
			for line in seedFile:
				seeds.append(line[:-1])
			return seeds
	
	start_urls  = getSeeds()
	
	rules = (	
		Rule(LinkExtractor(allow=(r'(\bnews\b)\D+(-\d+)$'),
						    deny=(r'.*(m\.|\.test\.|\.stage\.|%|comments|\/live\/|\/athlete\/|\/weather\/).*'),
							restrict_xpaths="//body"),
			 				callback='parseNews',
			 				follow=True),
		
		Rule(LinkExtractor(allow=(r'(\b\/story\/\b)\d+(-\D+)$'),
						    deny=(r'.*(m\.|\.test\.|\.stage\.|%|comments|\/live\/|\/athlete\/|\/weather\/).*'),
							restrict_xpaths="//body"),
			 				callback='parseStory',
			 				follow=True),
		
		Rule(LinkExtractor(allow=(r'(\b\/sport\/\b)\D+(\d+)$'),
						    deny=(r'.*(m\.|\.test\.|\.stage\.|%|comments|\/live\/|\/athlete\/|\/weather\/).*'),
							restrict_xpaths="//body"),
			 				callback='parseSport',
			 				follow=True),
		
		Rule(LinkExtractor(allow=(),
						    deny=(r'.*(m\.|\.test\.|\.stage\.|%|comments|\/live\/|\/athlete\/|\/weather\/).*')),
			 				follow=True),
    )
	
	def parseNews(self, response):
		item = WebSite()
		item['url'] = response.url		
		
		tmp_year = response.xpath("//@data-seconds")
		
		if tmp_year is not None and len(tmp_year) > 0:
			item['year'] = datetime.datetime.fromtimestamp(int(tmp_year[0].extract())).year

		self.logItem(response, item)

	
	def parseStory(self, response):
		item = WebSite()
		item['url'] = response.url	

		tmp_year = response.xpath("//span[@class='publication-date index-body']//text()")
		
		if tmp_year is not None and len(tmp_year) > 0:
			item['year'] = int(tmp_year[0].extract()[-4:])

		self.logItem(response, item)
	
	def parseSport(self, response):
		item = WebSite()
		item['url'] = response.url

		tmp_year = response.xpath("//@data-timestamp")
		
		if tmp_year is not None and len(tmp_year) > 0:
			item['year'] = datetime.datetime.fromtimestamp(int(tmp_year[0].extract())).year

		self.logItem(response, item)

		self.logItem(response, item)

		
	def logItem(self, response, item):
		if item.get('year') is None:
			json_ld_script = response.xpath('//script[@type="application/ld+json"]/text()').get()

			if json_ld_script:
				json_data = json.loads(json_ld_script)
				date_modified = json_data.get('dateModified', json_data.get('uploadDate'))

				if date_modified is None:
					return 
				# print('datetimeVVVVVV')
				# print(date_modified[:-1])
				# print(datetime.datetime.fromisoformat(date_modified[:-1]))
				# print('datetime^^^^^^')
				item['year'] = datetime.datetime.fromisoformat(date_modified[:-1]).year

		if item.get('year') is not None:
			if item['year'] <= 24:
				item['year'] += 2000
				
			if item['year'] >= 2012 and item['year'] <= 2023:
				if item['year'] not in self.visitedUrls:
					self.visitedUrls[item['year']] = []

				self.visitedUrls[item['year']].append(item['url'])
				self.logProgress()
				return item

	def logProgress(self):
		now = datetime.datetime.now()
		if now.replace(hour=21, minute=0, second=0, microsecond=0) <= now:
			if self.savedToday is False:

				for year in self.visitedUrls:
					with open(f"visitedUrls_{year}_{datetime.datetime.strftime(now, '%Y-%m-%d %H:%M:%S')}.txt", 'a') as f:
						for url in self.visitedUrls[year]:
							f.write(url+"\n")
						f.close()

				self.savedToday = True
		else:
			self.savedToday = False

	
	def spiderClosed(self, spider):		
		for year in self.visitedUrls:
			with open(f"visitedUrls_{year}.txt", 'a') as f:
				for url in self.visitedUrls[year]:
					f.write(url+"\n")
				f.close()
