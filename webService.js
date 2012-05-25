var sys = require("util"),
	http = require("http"),
	url = require("url"),
	path = require("path"),
	querystring = require("querystring"),
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
      new (winston.transports.File)({ filename: logPath + 'webservice.log', json : false, timestamp : true })
    ]
  });

var localServicePort = 12243;

//Subscribe a user, or update an existing user
function SubscribeRequest(subResponse, userName, parsedUrl, requestData) {
	logger.info("Subscribe Called.");

	try {

		//If we haven't got a username, just give up now.
		if(userName.length == 0)
		{
			subResponse.writeHead(404, { "Content-Type": "text/plain" });
			logger.info("Attempt to create a subscription for a blank user. That's no good.");
			subResponse.end("Not found.");
		}

		var saveObject = {
			//Shacknews user name.
			userName: userName,
			//Date this request was updated.
			dateCreated: new Date(),
			//Count of replies when the app last talked with us.
			replyCount: 0,
			//Count of replies on the last time we notified.
			//This will always be reset the current number of replies when the app refreshes.
			replyCountLastNotified: 0,
			//URI to send notification data to.
			notificationUri: requestData,
			//Unique identifier for the device.
			deviceId: '',
			//1 = Tile only, 2 = Tile and Toast
			notificationType: 1
		};

		var siteUrl = url.parse(apiBaseUrl + apiParentAuthorQuery + userName);

		var requestOptions = {
			host: siteUrl.host,
			port: 80,
			path: siteUrl.path
		};

		http.get(requestOptions, function (res) {
			var dataReceived = '';
			var errorOccurred = false;
			res.on('data', function (chunk) {
				dataReceived += chunk;
			});
			res.on('error', function(err) {
				errorOccurred = true;
				logger.error('Error occurred trying to retrieve reply count for ' + userName + '.\n!!ERROR!!: ' + err);
			});
			res.on('end', function () {
				try {

					var xmlDoc = libxmljs.parseXmlString(dataReceived);

					if (xmlDoc == null) return;

					var totalResultsAttribute = xmlDoc.root().attr('total_results');
					if (totalResultsAttribute == null) return;

					//This is the number of results we know about right this very moment.
					//Not relying on the app to tell us any more, it's up to us.
					var totalResults = parseInt(totalResultsAttribute.value());

					saveObject.replyCount = totalResults;
					saveObject.replyCountLastNotified = totalResults;

					if (parsedUrl.hasOwnProperty('query')) {
						var parsedQuery = querystring.parse(parsedUrl.query);
						logger.info("Parsed query: " + JSON.stringify(parsedQuery));

						if (parsedQuery.hasOwnProperty('notificationType')) {
							saveObject.notificationType = parsedQuery['notificationType'];
						} else {
							subResponse.writeHead(400, { "Content-Type": "text/plain" });
							logger.info("Missing notification type.");
							subResponse.end("Missing notification type.");
							return;
						}
						if (parsedQuery.hasOwnProperty('deviceId')) {
							saveObject.deviceId = parsedQuery['deviceId'];
						} else {
							subResponse.writeHead(400, { "Content-Type": "text/plain" });
							logger.info("Missing device id.");
							subResponse.end("Missing device id.");
							return;
						}
					}

					logger.info("Subscribing with info: " + JSON.stringify(saveObject));

					//Make sure the user has less than 5 devices, otherwise we'll replace the oldest one.
					//TODO: Replace the oldest one.
					//			subResponse.writeHead(400, { "Content-Type": "text/plain" });
					//			subResponse.end("Too many devices.");
					//			return;

					var savePath = path.join(subscriptionDirectory, saveObject.deviceId);
					logger.info("Saving data to " + savePath);

					fs.writeFileSync(savePath, JSON.stringify(saveObject));
					logger.info("Saved file!");
					subResponse.writeHead(200, { "Content-Type": "text/plain" });
					subResponse.end("Subscribed " + userName);
				} catch (ex) {
					logger.error('Error occurred in response end for user ' + userName + '.\n!!ERROR!!: ' + ex);
					subResponse.writeHead(400, { "Content-Type": "text/plain" });
					subResponse.end("Unknown error.");
				}
			});
		}).on('error', function(ex) {
			logger.error('Error occurred trying to retrieve reply count for ' + userName + '.\n!!ERROR!!: ' + ex);
			subResponse.writeHead(400, { "Content-Type": "text/plain" });
			subResponse.end("Unknown error.");
		});
	}
	catch (ex) {
		logger.info("Exception caught in subscription " + ex);
		subResponse.writeHead(400, { "Content-Type": "text/plain" });
		subResponse.end("Unknown error.");
	}
}


//Remove a user
function RemoveRequest(response, parsedUrl, userName) {
	if (parsedUrl.hasOwnProperty('query')) {
		var parsedQuery = querystring.parse(parsedUrl.query);
		if (parsedQuery.hasOwnProperty('deviceId')) {
			var file = path.join(subscriptionDirectory, parsedQuery['deviceId']);
			path.exists(file, function (exists) {
				if (exists) {
					fs.unlinkSync(file);
					logger.info('Request for removal of ' + userName + ' successful.');
				}
			});
		}
		else {
			response.writeHead(400, { "Content-Type": "text/plain" });
			response.end("Missing device id.");
			return;
		}
	} else {
		response.writeHead(400, { "Content-Type": "text/plain" });
		response.end("Bad request.");
		return;
	}

	response.writeHead(200, { "Content-Type": "text/plain" });
	response.end("Removed " + userName);
}

//Create the server - this is where the magic happens.
http.createServer(function (request, response) {
	var requestData = '';
	request.on('data', function (chunk) {
		requestData += chunk;
	});
	request.on('end', function () {
		var parsedUrl = url.parse(request.url);
		var splitPath = parsedUrl.pathname.split('/');

		logger.info('Parsed URL: ' + JSON.stringify(parsedUrl));

		if (splitPath.length > 3) {
			logger.info('more than two path variables were passed, bailing.');
			return;
		}

		if(splitPath[1] == 'users') {
			if(request.method == 'POST') {
				SubscribeRequest(response, splitPath[2], parsedUrl, requestData);
			} else if (request.method == 'DELETE') {
				RemoveRequest(response, parsedUrl, splitPath[2]);
			}
		}
		else {
			response.writeHead(404, { "Content-Type": "text/plain" });
			response.end("404 Not Found\n");
			return;
		}
	});
}).listen(localServicePort);

logger.info("Server running at http://localhost:" + localServicePort);
logger.info("rootPath = " + rootPath);
logger.info("logPath = " + logPath);
logger.info("subscriptionDirectory = " + subscriptionDirectory);
logger.info("apiBaseUrl = " + apiBaseUrl);
logger.info("apiParentAuthorQuery = " + apiParentAuthorQuery);
