sys = require 'util'
http = require 'http'
https = require 'https'
url = require 'url'
path = require 'path'
fs = require 'fs'
jsxml = require 'node-jsxml'
winston = require 'winston'
XML = jsxml.XML

#Convert these to use the config file.
rootPath = __dirname
logPath = path.join(rootPath, 'logs/')
subscriptionDirectory = path.join(rootPath, 'subscribedUsers/')
apiBaseUrl = 'http://shackapi.stonedonkey.com/'
apiParentAuthorQuery = 'Search/?ParentAuthor='
configFile = ".notificationConfig"

logger = new (winston.Logger)({
	transports: [
		new (winston.transports.Console)( { colorize: true, timestamp : true, level : 'silly'} ),
		new (winston.transports.File)({ filename: logPath + 'processor.log', json : false, timestamp : true, level : 'silly'})
		]
	})

class Config
	constructor: () ->
		try
			parsedConfig = JSON.parse(fs.readFileSync(configFile))
			@psid = parsedConfig['PSID']
			@secret = parsedConfig["Secret"]
		catch error
			logger.error("""Error parsing config file at #{configFile}.  Format is:
			{
				"PSID": "ProductSecurityIdentifier",
				"Secret": "ClientSecret"
			}

			Error was: #{error}
			""")

class WNSAuthentication
	constructor: () ->
		@config = new Config()
	Authenticate: (@success, @error) =>
		unless(@success)
			throw 'Success method not defined for WNS Authentication.'
		unless(@error)
			throw 'Error method not defined for WNS Authentication.'

		requestData = "grant_type=client_credentials&client_id=#{encodeURIComponent(@config.psid)}&client_secret=#{@config.secret}&scope=notify.windows.com"

		options = {
			hostname: 'login.live.com',
			path: '/accesstoken.srf',
			method: 'POST',
			headers: {
				'Content-Length': requestData.length
				'Content-Type': 'application/x-www-form-urlencoded',
			}
		}

		request = https.request(options, (res) =>
			responseBody = ''
			res.setEncoding('utf8')
			res.on('data', (chunk) =>
				responseBody += chunk
			)
			res.on('end', =>
				if(res.statusCode is 200)
					authenticationResponse = JSON.parse(responseBody)
					@type = authenticationResponse['token_type']
					@expire = authenticationResponse['expires_in']
					@token = authenticationResponse['access_token']
					@success()
				else
					@error()
			)
			res.on('error', (e) =>
				logger.error("Error getting authentication data from WNS. [#{e}]")
				@error
			)
		)

		request.write(requestData)
		request.end()

class UserProcessor
	constructor: (@userData, @wnsConfig) ->

	SendWindows8Data: (requestOptions, payload, userInfo) =>
		request = https.request(requestOptions, (res) =>
			res.setEncoding('utf8')
			errorDescription = res.headers['X-WNS-Error-Description']

			responseBody = ''
			responseSuccessful = (res.statusCode is 200)	#and (notificationStatus is 'received')	and (deviceConnectionStatus is 'connected') and (subscriptionStatus is 'active')

			res.on('data', (chunk) => 
				responseBody += chunk
			)

			res.on('end', =>
				if (!responseSuccessful) 
					logger.info("""Sending push failed.
						Code: #{res.statusCode}
						Error: #{errorDescription}
						Response: #{responseBody}""")
					
				else 
					logger.info('Windows 8 notification sent successfully!')
			)
		)

		request.on('error', (e) =>
			logger.info('Request Failed: ' + e.message)
		)

		# write data to request body
		request.write(payload)
		request.end()

	SendWindows8Notification: (count, author, preview, userInfo) =>
		parsedUri = url.parse(userInfo.notificationUri)

		tileData = """<tile launch="">
		  <visual lang="en-US">
		    <binding template="TileWideText09">
		      <text id="1">#{author}</text>
		      <text id="2">#{preview}</text> 
		    </binding>
		  </visual>
		</tile>"""

		requestOptions = {
			hostname: parsedUri.hostname,
			port: parsedUri.port,
			path: parsedUri.path,
			method: 'POST',
			headers: {
				#TODO: Get the real authorization token from the service.
				'Authorization': "Bearer #{@wnsConfig.token}",
				'Content-Type': 'text/xml',
				'Content-Length': tileData.length,
				'X-WNS-Type': 'wns/tile'
			}
		}

		@SendWindows8Data(requestOptions, tileData, userInfo)

		tileData = """<tile launch="">
		  <visual lang="en-US">
		    <binding template="TileSquareText02">
		      <text id="1">#{author}</text>
		      <text id="2">#{preview}</text> 
		    </binding>
		  </visual>
		</tile>"""

		requestOptions = {
			hostname: parsedUri.hostname,
			port: parsedUri.port,
			path: parsedUri.path,
			method: 'POST',
			headers: {
				#TODO: Get the real authorization token from the service.
				'Authorization': "Bearer #{@wnsConfig.token}",
				'Content-Type': 'text/xml',
				'Content-Length': tileData.length,
				'X-WNS-Type': 'wns/tile'
			}
		}

		@SendWindows8Data(requestOptions, tileData, userInfo)

		tileData = """<badge value="#{count}" />"""

		requestOptions = {
			hostname: parsedUri.hostname,
			port: parsedUri.port,
			path: parsedUri.path,
			method: 'POST',
			headers: {
				#TODO: Get the real authorization token from the service.
				'Authorization': "Bearer #{@wnsConfig.token}",
				'Content-Type': 'text/xml',
				'Content-Length': tileData.length,
				'X-WNS-Type': 'wns/badge'
			}
		}	

		@SendWindows8Data(requestOptions, tileData, userInfo)

	SendWP7Notification: (requestOptions, payload, userInfo) =>
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
						logger.info("""Sending push failed.
							Code: #{res.statusCode}
							Notification Status: #{notificationStatus}
							Device Connection Status: #{deviceConnectionStatus}
							Subscription Status: #{subscriptionStatus}
							Body: #{responseBody}""")
					
				else 
					logger.info('WP7 notification sent successfully!')
			)
		)

		request.on('error', (e) =>
			logger.info('Request Failed: ' + e.message)
		)

		# write data to request body
		request.write(payload)
		request.end()

	SendWP7ToastNotification: (author, preview, userInfo) =>
		parsedUri = url.parse(userInfo.notificationUri)

		toastData = """<?xml version="1.0" encoding="utf-8"?>
	        <wp:Notification xmlns:wp="WPNotification">
	           <wp:Toast>
	              <wp:Text1>#{author}</wp:Text1>
	              <wp:Text2>#{preview}</wp:Text2>
	           </wp:Toast>
	        </wp:Notification>"""

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

	SendWP7TileNotification: (count, author, preview, userInfo) =>
		parsedUri = url.parse(userInfo.notificationUri)

		tileMessage = """<?xml version="1.0" encoding="utf-8"?>
			<wp:Notification xmlns:wp="WPNotification">
				 <wp:Tile>
					  <wp:Count>#{count}</wp:Count>
					  <wp:BackTitle>#{author}</wp:BackTitle>
					  <wp:BackContent>#{preview}</wp:BackContent>
				 </wp:Tile>
			</wp:Notification>"""

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

	ProcessUser: () =>
		siteUrl = url.parse(apiBaseUrl + apiParentAuthorQuery + @userData.userName)

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
				newReplyCount = parseInt(totalResults) - parseInt(@userData.replyCountLastNotified)
				if (newReplyCount > 0) 
					logger.info("Previous count for #{@userData.userName} was #{@userData.replyCount} current count is #{totalResults}, we got new stuff!")

					replies = xmlDoc.child('result')
					if(replies._list.length > 0)
						latestResult = replies._list[0]
						if (latestResult != null) 
							author = latestResult.attribute('author').toString()
							body = latestResult.child('body').toString().substr(0, 40)

							logger.verbose("Latest Author: #{author} Body: #{body}")
							logger.verbose("@userData #{JSON.stringify(@userData)}")
							if (@userData.hasOwnProperty('notificationUri')) 	
								if ((@userData.notificationType is '2') or (@userData.notificationType is '1'))
									#The count of new replies is the total number of current replies minus the number of replies the app last knew about
									@SendWP7TileNotification(parseInt(totalResults) - parseInt(@userData.replyCount), author, body, @userData)
									if (@userData.notificationType is '2') 
										@SendWP7ToastNotification(author, body, @userData)
								else if (@userData.notificationType is '3')
									@SendWindows8Notification(parseInt(totalResults) - parseInt(@userData.replyCount), author, body, @userData)
							else 
								logger.info('Would send push notification of\n  Author: ' + author + '\n  Preview: ' + body)

					#Since we got new stuff, it's time to update the current count.
					@userData.replyCountLastNotified = totalResults
					fileNameToSave = path.join(subscriptionDirectory, @userData.deviceId)
					fs.writeFile(fileNameToSave, JSON.stringify(@userData), (err) =>
						if (err)
							logger.info("Error saving file " + fileNameToSave + " " + err)
						else 
							logger.info("Saved updated file " + fileNameToSave + " for username " + @userData.userName + "!")
					)
				
				else 
					logger.info('No new replies for ' + @userData.userName + ', previous count notified at was ' + @userData.replyCountLastNotified + ' current count is ' + totalResults);
				
			)
		)

class Processor
	constructor: (@directoryToProcess) ->

	Process: () =>
		@wnsConfig = new WNSAuthentication()
		@wnsConfig.Authenticate(@ProcessDirectory, @AuthError)

	ProcessDirectory: () =>
		logger.info("Processing directory #{@directoryToProcess}")
		fs.readdir(@directoryToProcess, (err, files) =>
			for file in files
				logger.info('Processing file ' + file + ' in ' + @directoryToProcess)
				fileData = fs.readFileSync(path.join(@directoryToProcess, file), 'utf8')

				userData = JSON.parse(fileData)
				up = new UserProcessor(userData, @wnsConfig)
				up.ProcessUser()
		)

	AuthError: () =>
		logger.error('Could not authenticate with WNS.')

DirectoryExists = (dir) ->
	try 
		stats = fs.statSync(dir)
		return stats.isDirectory()
	catch ex 
		#logger.error("problem getting directory info " + ex);
		return false

p = new Processor(subscriptionDirectory)
p.Process()