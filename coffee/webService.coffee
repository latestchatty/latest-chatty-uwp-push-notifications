sys = require 'util'
http = require 'http'
url = require 'url'
path = require 'path'
querystring = require 'querystring'
fs = require 'fs'
jsxml = require 'node-jsxml'
winston = require 'winston'

rootPath = __dirname
logPath = path.join(rootPath, 'logs/')
subscriptionDirectory = path.join(rootPath, 'subscribedUsers/')

apiBaseUrl = 'http://shackapi.stonedonkey.com/'
apiParentAuthorQuery = 'Search/?ParentAuthor='

logger = new (winston.Logger)(
	transports: [
		new (winston.transports.Console)( { colorize: true, timestamp : true, level : 'silly' } ),
		new (winston.transports.File)({ filename: logPath + 'webservice.log', json : false, timestamp : true, level : 'silly'})
	]
)

#localServicePort = 12243 #production
localServicePort = 12253 #development

#Subscribe a user, or update an existing user
SubscribeRequest = (subResponse, userName, parsedUrl, requestData) =>
	logger.verbose("Subscribe Called.")

	try
		#If we haven't got a username, just give up now.
		#Already doing this outside this call, but I guess I'll just leave this here.
		if(userName.length == 0)
			subResponse.writeHead(404, { "Content-Type": "text/plain" });
			logger.warn("Attempt to create a subscription for a blank user. That's no good.");
			subResponse.end("Not found.");

		saveObject =
			#Shacknews user name.
			userName: userName,
			#Date this request was updated.
			dateCreated: new Date(),
			#Count of replies when the app last talked with us.
			replyCount: 0,
			#Count of replies on the last time we notified.
			#This will always be reset the current number of replies when the app refreshes.
			replyCountLastNotified: 0,
			#URI to send notification data to.
			notificationUri: requestData,
			#Unique identifier for the device.
			deviceId: '',
			#1 = Tile only, 2 = Tile and Toast
			notificationType: 1

		siteUrl = url.parse(apiBaseUrl + apiParentAuthorQuery + userName)

		requestOptions = 
			host: siteUrl.host,
			port: 80,
			path: siteUrl.path

		http.get(requestOptions, (res) =>
			dataReceived = ''
			errorOccurred = false
			res.on('data', (chunk) =>
				dataReceived += chunk
			)

			res.on('error', (err) =>
				errorOccurred = true
				logger.error('Error occurred trying to retrieve reply count for ' + userName + '.\n!!ERROR!!: ' + err)
			)

			res.on('end', () =>
				try 
					#I would prefer to switch to the json api, but it appears to not show the total_results.  It's null.
					xmlDoc = libxmljs.parseXmlString(dataReceived)

					if (xmlDoc is null) 
						return

					totalResultsAttribute = xmlDoc.root().attr('total_results')
					if (totalResultsAttribute is null) 
						return

					#This is the number of results we know about right this very moment.
					#Not relying on the app to tell us any more, it's up to us.
					totalResults = parseInt(totalResultsAttribute.value())

					saveObject.replyCount = totalResults
					saveObject.replyCountLastNotified = totalResults

					if (parsedUrl.hasOwnProperty('query'))
						parsedQuery = querystring.parse(parsedUrl.query)
						logger.silly("Parsed query: " + JSON.stringify(parsedQuery))

						if (parsedQuery.hasOwnProperty('notificationType'))
							saveObject.notificationType = parsedQuery['notificationType'];
						else
							subResponse.writeHead(400, { "Content-Type": "text/plain" })
							logger.error("Missing notification type.")
							subResponse.end("Missing notification type.")
							return
						
						if (parsedQuery.hasOwnProperty('deviceId'))
							saveObject.deviceId = parsedQuery['deviceId']
						else
							subResponse.writeHead(400, { "Content-Type": "text/plain" })
							logger.error("Missing device id.")
							subResponse.end("Missing device id.")
							return
					

					logger.silly("Subscribing with info: " + JSON.stringify(saveObject))

					#Make sure the user has less than 5 devices, otherwise we'll replace the oldest one.
					#TODO: Replace the oldest one.
					#			subResponse.writeHead(400, { "Content-Type": "text/plain" });
					#			subResponse.end("Too many devices.");
					#			return;

					savePath = path.join(subscriptionDirectory, saveObject.deviceId)
					logger.verbose("Saving data to " + savePath)

					fs.writeFileSync(savePath, JSON.stringify(saveObject))
					logger.info("Updated subscription for " + userName + " with device id " + saveObject.deviceId)
					subResponse.writeHead(200, { "Content-Type": "text/plain" })
					subResponse.end("Subscribed " + userName)
				catch ex
					logger.error('Error occurred in response end for user ' + userName + '.\n!!ERROR!!: ' + ex)
					subResponse.writeHead(400, { "Content-Type": "text/plain" })
					subResponse.end("Unknown error.")
			)
		).on('error', (ex) =>
			logger.error('Error occurred trying to retrieve reply count for ' + userName + '.\n!!ERROR!!: ' + ex)
			subResponse.writeHead(400, { "Content-Type": "text/plain" })
			subResponse.end("Unknown error.")
		)
	catch ex
		logger.error("Exception caught in subscription " + ex)
		subResponse.writeHead(400, { "Content-Type": "text/plain" })
		subResponse.end("Unknown error.")


#Remove a user
RemoveRequest = (response, parsedUrl, userName) =>
	if (parsedUrl.hasOwnProperty('query')) 
		parsedQuery = querystring.parse(parsedUrl.query)
		if (parsedQuery.hasOwnProperty('deviceId')) 
			deviceId = parsedQuery['deviceId']
			file = path.join(subscriptionDirectory, parsedQuery['deviceId'])
			if(path.existsSync(file))
				fileData = fs.readFileSync(file, 'utf8')
				userData = JSON.parse(fileData)
				if(userName.toLowerCase() == userData.userName.toLowerCase())
					fs.unlinkSync(file)

					logger.info("Removed deviceId " + deviceId + " for " + userName)
					response.writeHead(200, { "Content-Type": "text/plain" })
					response.end("Removed " + userName)
					return
				else
					logger.error("DeviceId data " + deviceId + " does not match userName " + userName + " cannot remove device registration")
			else
				#If the file doesn't exist, we've succeeded in what we were trying to do anyway.
				response.writeHead(200, { "Content-Type": "text/plain" })
				response.end("Removed " + userName)
				return
		else
			logger.error("Missing device id on removal request")
	else
		logger.error("Missing query parameter on removal request")

	response.writeHead(400, { "Content-Type": "text/plain" })
	response.end("Bad request.")

#Create the server - this is where the magic happens.
http.createServer((request, response) =>
	requestData = ''
	request.on('data', (chunk) =>
		requestData += chunk;
	)
	request.on('end', () =>
		logger.verbose("Request recieved: #{requestData}")
		parsedUrl = url.parse(request.url)
		splitPath = parsedUrl.pathname.split('/')
		requestHandled = false

		logger.verbose('Parsed URL: ' + JSON.stringify(parsedUrl))
		logger.verbose('Split Path: ' + JSON.stringify(splitPath))
		logger.verbose('Split Path Length: ' + splitPath.length)

		if (splitPath.length > 3)
			logger.error('more than two path variables were passed, bailing.')
			return

		if(splitPath.length == 3)
			#Make sure we're going to /users and that the username is something valid.
			if((splitPath[1] == 'users') && (splitPath[2].replace(" ", "").length > 0))
				if(request.method == 'POST')
					requestHandled = true
					SubscribeRequest(response, splitPath[2], parsedUrl, requestData)
				else if (request.method == 'DELETE')
					requestHandled = true
					RemoveRequest(response, parsedUrl, splitPath[2])
		
		if(!requestHandled)
			logger.error("Request not handled.")
			response.writeHead(404, { "Content-Type": "text/plain" })
			response.end("404 Not Found\n")
			return
	)
).listen(localServicePort)

logger.info("Server running at http://localhost:" + localServicePort)
logger.verbose("rootPath = " + rootPath)
logger.verbose("logPath = " + logPath)
logger.verbose("subscriptionDirectory = " + subscriptionDirectory)
logger.verbose("apiBaseUrl = " + apiBaseUrl)
logger.verbose("apiParentAuthorQuery = " + apiParentAuthorQuery)