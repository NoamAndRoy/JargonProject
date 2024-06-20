import scrapy
import os
import re
from string import printable
from scrapy.signalmanager import SignalManager
from scrapy import signals
from pydispatch import dispatcher
from items import WebSite

class GeneralUrls(scrapy.Spider):
	name = "BBCWordsSpider"
	allowed_domains = ["bbc.com"]
	
	generalWords = {}
	
	splitOptions = []
	visitedUrls = []
	
	savedToday = False

	custom_settings = {
        # 'CLOSESPIDER_PAGECOUNT': 120,
		'CLOSESPIDER_ITEMCOUNT': 100,
    }

	def __init__(self, *args, **kwargs):
		super(GeneralUrls, self).__init__(*args, **kwargs)
		self.next_counter = 1
		self.splitOptions.append(os.linesep)
		os.makedirs(f'article_files_{self.year}')
		
		for c in printable:
			if c.isalpha() == False and c != '\'':
				self.splitOptions.append(c)

		self.start_urls  = self.getSeeds()
	
	def getSeeds(self):
		seeds = []
		
		with open(os.path.join(os.path.dirname(__file__), f'generalUrls_{self.year}.txt'), 'r') as seedFile:
			for line in seedFile:
				seeds.append(line[:-1])
			return seeds

	def parse(self, response):
		if re.search(r'(\b\/sport\/\b)\D+(\d+)$', response.url, re.I) or re.search(r'(\bnews\b)\D+(-\d+)$', response.url, re.I):
			self.parseNews(response)
		elif re.search(r'(\b\/story\/\b)\d+(-\D+)$', response.url, re.I):
			self.parseStory(response)
	
	def parseNews(self, response):	
		item = WebSite()
		self.visitedUrls.append(response.url)
		item['content'] = ''

		for sel in response.xpath('//main[@id="main-content"]//article//div[@data-component="text-block"]//p//text()'):
			item['content'] += sel.extract().strip()

		self.SaveArticle(item['content'], f'article_files_{self.year}')
		return item
	
	def parseStory(self, response):
		item = WebSite()
		self.visitedUrls.append(response.url)
		item['content'] = ''

		for sel in response.xpath('//*[@class="body-content"]//p//text()'):
			item['content'] += sel.extract().strip()

		self.SaveArticle(item['content'], f'article_files_{self.year}')
		return item

		
	def SaveArticle(self,text, directory=None):
		self.next_counter += 1

		# Create the filename
		filename = os.path.join(directory, f'article_{self.next_counter}.txt')

		# Write the text to the file
		with open(filename, 'w+') as file:
			file.write(text)

		return filename
						