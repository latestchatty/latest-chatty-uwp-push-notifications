var sys = require("util"),
	http = require("http"),
	url = require("url"),
	path = require("path"),
	fs = require("fs"),
	libxmljs = require("libxmljs"),
	winston = require("winston");

var rootPath = __dirname;
var logPath = path.join(rootPath, 'logs/');
var subscriptionDirectory = path.join(rootPath, 'subscribedUsers/');
var apiBaseUrl = 'http://shackapi.stonedonkey.com/';
var apiParentAuthorQuery = 'Search/?ParentAuthor=';

var logger = new (winston.Logger)({
    transports: [
      new (winston.transports.Console)( { colorize: true, timestamp : true } ),
      new (winston.transports.File)({ filename: logPath + 'processor.log', json : false, timestamp : true, level : 'silly'})
    ]
  });

function SendWP7Notification(requestOptions, payload, userInfo) {
	var request = http.request(requestOptions, function (res) {
		var notificationStatus = res.headers['x-notificationstatus'].toLowerCase();
		var deviceConnectionStatus = res.headers['x-deviceconnectionstatus'].toLowerCase();
		var subscriptionStatus = res.headers['x-subscriptionstatus'].toLowerCase();

		res.setEncoding('utf8');

		var responseBody = '';
		var responseSuccessful = (res.statusCode == 200)
				&& (notificationStatus == 'received')
				&& (deviceConnectionStatus == 'connected')
				&& (subscriptionStatus == 'active');

		res.on('data', function (chunk) {
			responseBody += chunk;
		});
		res.on('end', function () {
			if (!responseSuccessful) {
				//TODO: Handle failures better.
				//  There are cases where we should retry immediately, retry later, never try again, etc.
				//  As it stands, if we fail to send, we'll never retry.
				//  Especially need to pay attention to when a device channel is no longer valid.
				//  Otherwise we're just wasting time trying to notify something that will never, ever get it.
				if(subscriptionStatus == 'expired') {
					var file = path.join(subscriptionDirectory, userInfo.deviceId);
					path.exists(file, function (exists) {
						if (exists) {
							fs.unlinkSync(file);
							logger.info('Device ID ' + userInfo.deviceId + ' for user ' + userInfo.userName + ' has expired.  Removing subscription.');
						}
					});
				} else {
					logger.info('Sending push failed.');
					logger.info('Code: ' + res.statusCode);
					logger.info('Notification Status: ' + notificationStatus);
					logger.info('Device Connection Status: ' + deviceConnectionStatus);
					logger.info('Subscription Status: ' + subscriptionStatus);
					logger.info('Body: ' + responseBody);
				}
			}
			else {
				logger.info('WP7 notification sent successfully!');
			}
		});
	});

	request.on('error', function (e) {
		logger.info('Request Failed: ' + e.message);
	});

	// write data to request body
	request.write(payload);
	request.end();
}

function SendWP7ToastNotification(author, preview, userInfo) {
	var parsedUri = url.parse(userInfo.notificationUri);

	var toastData = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<wp:Notification xmlns:wp=\"WPNotification\">" +
           "<wp:Toast>" +
              "<wp:Text1>" + author + "</wp:Text1>" +
              "<wp:Text2>" + preview + "</wp:Text2>" +
           "</wp:Toast>" +
        "</wp:Notification>";

	var requestOptions = {
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
	};

	logger.info('**Sending toast notification\n' + toastData);
	SendWP7Notification(requestOptions, toastData, userInfo);
}

function SendWP7TileNotification(count, author, preview, userInfo) {
	var parsedUri = url.parse(userInfo.notificationUri);

	var tileMessage = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
		"<wp:Notification xmlns:wp=\"WPNotification\">" +
			 "<wp:Tile>" +
				  "<wp:Count>" + count + "</wp:Count>" +
				  "<wp:BackTitle>" + author + "</wp:BackTitle>" +
				  "<wp:BackContent>" + preview + "</wp:BackContent>" +
			 "</wp:Tile>" +
		"</wp:Notification>";

	var requestOptions = {
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
	};

	logger.info('**Sending tile notification\n' + tileMessage);
	SendWP7Notification(requestOptions, tileMessage, userInfo);
}

function ProcessUser(userInfo) {
	var siteUrl = url.parse(apiBaseUrl + apiParentAuthorQuery + userInfo.userName);

	var requestOptions = {
		host: siteUrl.host,
		port: 80,
		path: siteUrl.path
	};

	http.get(requestOptions, function (response) {
		var dataReceived = '';
		response.on('data', function (chunk) {
			dataReceived += chunk;
		});
		response.on('end', function () {
			var xmlDoc = libxmljs.parseXmlString(dataReceived);

			if (xmlDoc == null) return;

			var totalResultsAttribute = xmlDoc.root().attr('total_results');
			if (totalResultsAttribute == null) return;

			var totalResults = totalResultsAttribute.value();

			//The count of new replies is the total number of current replies minus the number of replies since the last time we notified.
			var newReplyCount = parseInt(totalResults) - parseInt(userInfo.replyCountLastNotified);
			if (newReplyCount > 0) {
				logger.info('Previous count for' + userInfo.userName + ' was ' + userInfo.replyCount + ' current count is ' + totalResults + ', we got new stuff!');

				var latestResult = xmlDoc.get('//result');
				if (latestResult != null) {
					var author = latestResult.attr('author').value();
					var body = latestResult.get('body').text().substr(0, 40);

					if (userInfo.hasOwnProperty('notificationUri')) {
						if (userInfo.notificationType == 2) {
							SendWP7ToastNotification(author, body, userInfo);
						}
						//The count of new replies is the total number of current replies minus the number of replies the app last knew about
						SendWP7TileNotification(parseInt(totalResults) - parseInt(userInfo.replyCount), author, body, userInfo);
					}
					else {
						logger.info('Would send push notification of\n  Author: ' + author + '\n  Preview: ' + body);
					}
				}

				//Since we got new stuff, it's time to update the current count.
				userInfo.replyCountLastNotified = totalResults;
				var fileNameToSave = path.join(subscriptionDirectory, userInfo.deviceId);
				fs.writeFile(fileNameToSave, JSON.stringify(userInfo), function (err) {
					if (err) { logger.info("Error saving file " + fileNameToSave + " " + err); }
					else { logger.info("Saved updated file " + fileNameToSave + " for username " + userInfo.userName + "!"); }
				});
			}
			else {
				logger.info('No new replies for ' + userInfo.userName + ', previous count notified at was ' + userInfo.replyCountLastNotified + ' current count is ' + totalResults);
			}
		});
	});
}

function DirectoryExists(dir) {
	try {
		var stats = fs.statSync(dir);
		return stats.isDirectory();
	}
	catch (ex) {
		//logger.error("problem getting directory info " + ex);
		return false;
	}
}

function ProcessDirectory(dir) {
	logger.info("Processing directory " + dir);
	fs.readdir(dir, function (err, files) {
		for (var iFile = 0; iFile < files.length; iFile++) {
			logger.info('Processing file ' + files[iFile] + ' in ' + dir);
			var fileData = fs.readFileSync(path.join(dir, files[iFile]), 'utf8');

			var userData = JSON.parse(fileData);

			ProcessUser(userData);
		}
	});
}

ProcessDirectory(subscriptionDirectory);