import scrapy
import os
import re
import csv
import datetime
from string import printable
from scrapy.spiders import Rule
from scrapy.linkextractors import LinkExtractor
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

	def __init__(self, *args, **kwargs):
		super(GeneralUrls, self).__init__(*args, **kwargs)
		SignalManager(dispatcher.Any).connect(self.spiderClosed, signals.spider_closed)
		
		self.splitOptions.append(os.linesep)
		
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
	
	def split(self, txt, seps):
		default_sep = seps[0]

		for sep in seps[1:]:
			txt = txt.replace(sep, default_sep)
			
		return [i.strip() for i in txt.split(default_sep)]
	
	def cleanWord(self, word):
		cleanWord = ""
		word = word.lower()
		
		if word.endswith("'s"):
			word = word[:-2]
			
		if word.endswith("'"):
			word = word[:-1]
		
		for i, c in enumerate(word):
			if c.islower() or (c == "'" and i < len(word) - 1):
				cleanWord += c
				
		return cleanWord
	
	def updateWordsCount(self, itemContent, wordsDict):
		words = self.split(itemContent, list(' '))
		cleanWords = []
		
		for word in words:
			if "www" not in word and "@" not in word:
				cleanWords.append(word)
					  
		words = self.split(" ".join(cleanWords), self.splitOptions)
					  
		for word in words:
			word = self.cleanWord(word)

			if word != "":
				if word in wordsDict.keys():
					wordsDict[word] = wordsDict[word]+1
				else:
					wordsDict[word] = 1
					
	def parse(self, response):
		if re.search(r'(\b\/sport\/\b)\D+(\d+)$', response.url, re.I) or re.search(r'(\bnews\b)\D+(-\d+)$', response.url, re.I):
			self.parseNews(response)
		elif re.search(r'(\b\/story\/\b)\d+(-\D+)$', response.url, re.I):
			self.parseStory(response)
	
	def parseNews(self, response):	
		wordsDict = self.generalWords
		
		item = WebSite()
		self.visitedUrls.append(response.url)
		item['content'] = ''

		for sel in response.xpath('//main[@id="main-content"]//article//div[@data-component="text-block"]//p//text()'):
			item['content'] += sel.extract().strip()#.encode('ascii','ignore')

		# print('\n\n\n---------------------------------------------\n\n\n')
		# print(item['content'])
		# print('\n\n\n---------------------------------------------\n\n\n')

		self.updateWordsCount(item['content'], wordsDict)
		self.logProgress()
		
		return item
	
	def parseStory(self, response):
		wordsDict = self.generalWords
				
		item = WebSite()
		self.visitedUrls.append(response.url)
		item['content'] = ''

		for sel in response.xpath('//*[@class="body-content"]//p//text()'):
			item['content'] += sel.extract().strip()#.encode('ascii','ignore')

		self.updateWordsCount(item['content'], wordsDict)
		self.logProgress()
		
		return item

							  
	def logProgress(self):
		now = datetime.datetime.now()
		if now.replace(hour=1, minute=0, second=0, microsecond=0) <= now:
			if self.savedToday is False:
				self.writeSortedDictToFile(f"GeneralData_{self.year}_{datetime.datetime.strftime(datetime.datetime.now(), '%Y-%m-%d %H-%M-%S')}.csv", self.generalWords)
				with open(f"visitedUrls_{self.year}_{datetime.datetime.strftime(datetime.datetime.now(), '%Y-%m-%d %H-%M-%S')}.txt",'a') as f:
					for url in self.visitedUrls:
						f.write(url + '\n')
					f.close()
				self.savedToday = True
		else:
			self.savedToday = False

	def writeSortedDictToFile(self, fileName, dictName):
		with open(fileName,'a') as f:
			writer = csv.writer(f, lineterminator='\n')

			sortedKeysByValues = sorted(dictName, key = dictName.__getitem__, reverse = True)

			for key in sortedKeysByValues:
				writer.writerow((key, dictName[key]))
			f.close()
	def spiderClosed(self, spider):		
		#self.writeSortedDictToFile("ScienceData.csv", self.scienceWords)

		self.writeSortedDictToFile(f"GeneralData_{self.year}.csv", self.generalWords)
		with open(f"visitedUrls_{self.year}.txt",'a') as f:
			for url in self.visitedUrls:
				f.write(url + '\n')
			f.close()
	#self.crawler.engine.close_spider(BBCSpiders, 'cancelled') #stop spider
