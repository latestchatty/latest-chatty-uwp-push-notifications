sys = require 'util'
http = require 'http'
url = require 'url'
path = require 'path'
fs = require 'fs'
jsxml = require 'node-jsxml'
winston = require 'winston'
XML = jsxml.XML

rootPath = __dirname
logPath = path.join(rootPath, 'logs/')
subscriptionDirectory = path.join(rootPath, 'subscribedUsers/')
apiBaseUrl = 'http://shackapi.stonedonkey.com/'
apiParentAuthorQuery = 'Search/?ParentAuthor='

logger = new (winston.Logger)({
	transports: [
		new (winston.transports.Console)( { colorize: true, timestamp : true, level : 'silly'} ),
		new (winston.transports.File)({ filename: logPath + 'processor.log', json : false, timestamp : true, level : 'silly'})
		]
	})

SendWindows8Notification = (count, author, preview, userInfo) ->
	logger.verbose("Sending Windows 8 Notification...")

SendWP7Notification = (requestOptions, payload, userInfo) ->
	request = http.request(requestOptions, (res) =>
		notificationStatus = res.headers['x-notificationstatus'].toLowerCase()
		deviceConnectionStatus = res.headers['x-deviceconnectionstatus'].toLowerCase()
		subscriptionStatus = res.headers['x-subscriptionstatus'].toLowerCase()

		res.setEncoding('utf8')

		responseBody = ''
		responseSuccessful = (res.statusCode is 200)	and (notificationStatus is 'received')	and (deviceConnectionStatus is 'connected') and (subscriptionStatus is 'active')

		res.on('data', (chunk) => 
			responseBody += chunk
		)

		res.on('end', =>
			if (!responseSuccessful) 
				#TODO: Handle failures better.
				#  There are cases where we should retry immediately, retry later, never try again, etc.
				#  As it stands, if we fail to send, we'll never retry.
				#  Especially need to pay attention to when a device channel is no longer valid.
				#  Otherwise we're just wasting time trying to notify something that will never, ever get it.
				if(subscriptionStatus == 'expired') 
					file = path.join(subscriptionDirectory, userInfo.deviceId)
					path.exists(file, (exists) =>
						if (exists) 
							fs.unlinkSync(file)
							logger.info('Device ID ' + userInfo.deviceId + ' for user ' + userInfo.userName + ' has expired.  Removing subscription.')
					)
				else 
					logger.info('Sending push failed.')
					logger.info('Code: ' + res.statusCode)
					logger.info('Notification Status: ' + notificationStatus)
					logger.info('Device Connection Status: ' + deviceConnectionStatus)
					logger.info('Subscription Status: ' + subscriptionStatus)
					logger.info('Body: ' + responseBody)
				
			else 
				logger.info('WP7 notification sent successfully!')
		)
	)

	request.on('error', (e) 
		logger.info('Request Failed: ' + e.message)
	)

	# write data to request body
	request.write(payload)
	request.end()

SendWP7ToastNotification = (author, preview, userInfo) ->
	parsedUri = url.parse(userInfo.notificationUri)

	toastData = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<wp:Notification xmlns:wp=\"WPNotification\">" +
           "<wp:Toast>" +
              "<wp:Text1>#{author}</wp:Text1>" +
              "<wp:Text2>#{preview}</wp:Text2>" +
           "</wp:Toast>" +
        "</wp:Notification>"

	requestOptions = {
		hostname: parsedUri.hostname,
		port: parsedUri.port,
		path: parsedUri.path,
		method: 'POST',
		headers: {
			'Content-Type': 'text/xml',
			'Content-Length': toastData.length,
			'X-WindowsPhone-Target': 'toast',
			'X-NotificationClass': '2'
		}
	}

	logger.info('**Sending toast notification\n' + toastData)
	SendWP7Notification(requestOptions, toastData, userInfo)

SendWP7TileNotification = (count, author, preview, userInfo) ->
	parsedUri = url.parse(userInfo.notificationUri)

	tileMessage = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
		"<wp:Notification xmlns:wp=\"WPNotification\">" +
			 "<wp:Tile>" +
				  "<wp:Count>#{count}</wp:Count>" +
				  "<wp:BackTitle>#{author}</wp:BackTitle>" +
				  "<wp:BackContent>#{preview}</wp:BackContent>" +
			 "</wp:Tile>" +
		"</wp:Notification>"

	requestOptions = {
		hostname: parsedUri.hostname,
		port: parsedUri.port,
		path: parsedUri.path,
		method: 'POST',
		headers: {
			'Content-Type': 'text/xml',
			'Content-Length': tileMessage.length,
			'X-WindowsPhone-Target': 'token',
			'X-NotificationClass': '1'
		}
	}

	logger.info('**Sending tile notification\n' + tileMessage)
	SendWP7Notification(requestOptions, tileMessage, userInfo)

ProcessUser = (userInfo) ->
	siteUrl = url.parse(apiBaseUrl + apiParentAuthorQuery + userInfo.userName)

	requestOptions = {
		host: siteUrl.host,
		port: 80,
		path: siteUrl.path
	}

	http.get(requestOptions, (response) =>
		dataReceived = ''
		response.on('data', (chunk) =>
			dataReceived += chunk
		)

		response.on('end', () =>
			#logger.verbose("Got data from service #{dataReceived}")
			xmlDoc = new XML(dataReceived)

			if (xmlDoc == null) 
				return

			totalResultsAttribute = xmlDoc.attribute('total_results')
			if (totalResultsAttribute == null) 
				return

			totalResults = totalResultsAttribute.toString()

			#The count of new replies is the total number of current replies minus the number of replies since the last time we notified.
			newReplyCount = parseInt(totalResults) - parseInt(userInfo.replyCountLastNotified)
			if (newReplyCount > 0) 
				logger.info("Previous count for #{userInfo.userName} was #{userInfo.replyCount} current count is #{totalResults}, we got new stuff!")

#TODO: Figure out how the hell to get the first result...
				replies = xmlDoc.child('result')
				logger.verbose("replies: #{replies}")
				replies.each((item, index) =>
					logger.verbose("item: #{item}")
				)
				#logger.verbose("Latest Result: #{latestResult}")
				if (latestResult != null) 
					author = latestResult.attribute('author').toString()
					body = latestResult.child('body').toString().substr(0, 40)

					logger.verbose("Latest Author: #{author} Body: #{body}")
					logger.verbose("UserInfo #{JSON.stringify(userInfo)}")
					if (userInfo.hasOwnProperty('notificationUri')) 	
						if ((userInfo.notificationType is 2) or (userInfo.notificationType is 1))
							#The count of new replies is the total number of current replies minus the number of replies the app last knew about
							SendWP7TileNotification(parseInt(totalResults) - parseInt(userInfo.replyCount), author, body, userInfo)
							if (userInfo.notificationType == 2) 
								SendWP7ToastNotification(author, body, userInfo)
						else if (userInfo.notificationType is 3)
							SendWindows8Notification(parseInt(totalResults) - parseInt(userInfo.replyCount), author, body, userInfo)
					else 
						logger.info('Would send push notification of\n  Author: ' + author + '\n  Preview: ' + body)

				#Since we got new stuff, it's time to update the current count.
				userInfo.replyCountLastNotified = totalResults
				fileNameToSave = path.join(subscriptionDirectory, userInfo.deviceId)
				fs.writeFile(fileNameToSave, JSON.stringify(userInfo), (err) =>
					if (err)
						logger.info("Error saving file " + fileNameToSave + " " + err)
					else 
						logger.info("Saved updated file " + fileNameToSave + " for username " + userInfo.userName + "!")
				)
			
			else 
				logger.info('No new replies for ' + userInfo.userName + ', previous count notified at was ' + userInfo.replyCountLastNotified + ' current count is ' + totalResults);
			
		)
	)

DirectoryExists = (dir) ->
	try 
		stats = fs.statSync(dir)
		return stats.isDirectory()
	catch ex 
		#logger.error("problem getting directory info " + ex);
		return false
	
ProcessDirectory = (dir) ->
	logger.info("Processing directory " + dir)
	fs.readdir(dir, (err, files) =>
		for file in files
			logger.info('Processing file ' + file + ' in ' + dir)
			fileData = fs.readFileSync(path.join(dir, file), 'utf8')

			userData = JSON.parse(fileData)

			ProcessUser(userData)
	)

ProcessDirectory(subscriptionDirectory)