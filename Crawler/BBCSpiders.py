import scrapy
import os
import re
import csv
import datetime
import operator
from string import printable
from scrapy.spiders import CrawlSpider, Rule
from scrapy.linkextractors import LinkExtractor
from scrapy.linkextractors.lxmlhtml import LxmlLinkExtractor
from scrapy.signalmanager import SignalManager
from scrapy import signals
from scrapy.xlib.pydispatch import dispatcher
from BBCSpider.items import WebSite

class BBCSpiders(CrawlSpider):
	name = "BBCSpiders"
	allowed_domains = ["bbc.com"]
	scienceCategories = []
	
	visitedUrlsGeneral = []
	visitedUrlsScience = []
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
						    deny=(r'.*(m\.|\.test\.|\.stage\.|%|comments|\/live\/|\/athlete\/|\/weather\/).*'),
						  	restrict_xpaths="//div[@role='main']"),
			 				follow=True),
    )
	
	def parseNews(self, response):
		science = False
		for url in self.scienceCategories:
			if url[:-1] in response.url:
				science = True
				break
		item = WebSite()
		item['url'] = response.url		
		item['year'] = datetime.datetime.fromtimestamp(int(response.xpath("//@data-seconds")[0].extract())).year
			
		if item['year'] >= 2012 and item['year'] <= 2015:
			if science is True:
				self.visitedUrlsScience.append(response.url)
			else:
				self.visitedUrlsGeneral.append(response.url)
			self.logProgress()
			return item
	
	def parseStory(self, response):
		science = False
		for url in self.scienceCategories:
			if url[:-1] in response.url:
				science = True
				break
		item = WebSite()
		item['url'] = response.url	
		item['year'] = int(response.xpath("//span[@class='publication-date index-body']//text()")[0].extract()[-4:])

		if item['year'] >= 2012 and item['year'] <= 2015:
			if science is True:
				self.visitedUrlsScience.append(response.url)
			else:
				self.visitedUrlsGeneral.append(response.url)	
			self.logProgress()
			return item
	
	def parseSport(self, response):
		
		item = WebSite()
		item['url'] = response.url
		item['year'] = datetime.datetime.fromtimestamp(int(response.xpath("//@data-timestamp")[0].extract())).year

		if item['year'] >= 2012 and item['year'] <= 2015:
			self.visitedUrlsGeneral.append(response.url)
			self.logProgress()
			return item

	def logProgress(self):
		now = datetime.datetime.now()
		if now.replace(hour=21, minute=0, second=0, microsecond=0) <= now:
			if self.savedToday is False:
				with open("visitedUrlsGeneral"+datetime.datetime.strftime(now, '%Y-%m-%d %H:%M:%S')+".txt", 'a') as f:
					for url in self.visitedUrlsGeneral:
						f.write(url+"\n")
					f.close()
				with open("visitedUrlsScience"+datetime.datetime.strftime(now, '%Y-%m-%d %H:%M:%S')+".txt", 'a') as f:
					for url in self.visitedUrlsScience:
						f.write(url+"\n")
					f.close()
				self.savedToday = True
		else:
			self.savedToday = False

	
	def spiderClosed(self, spider):		
		with open("visitedUrlsGeneral.txt", 'a') as f:
			for url in self.visitedUrlsGeneral:
				f.write(url+"\n")
			f.close()

		with open("visitedUrlsScience.txt", 'a') as f:
			for url in self.visitedUrlsScience:
				f.write(url+"\n")
			f.close()
