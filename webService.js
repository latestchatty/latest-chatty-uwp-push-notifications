var sys = require("util"),
	http = require("http"),
	url = require("url"),
	path = require("path"),
	querystring = require("querystring"),
	fs = require("fs"),
	libxmljs = require("libxmljs");

var subscriptionDirectory = path.join(process.cwd(), 'subscribedUsers');
var apiBaseUrl = 'http://shackapi.stonedonkey.com/';
var apiParentAuthorQuery = 'Search/?ParentAuthor=';

function DirectoryExists(dir) {
	try {
		var stats = fs.statSync(dir);
		return stats.isDirectory();
	}
	catch (ex) {
		return false;
	}
}

//Subscribe a user, or update an existing user
function SubscribeRequest(subResponse, userName, parsedUrl, requestData) {
	console.log("Subscribe Called.");

	try {

		//If we haven't got a username, just give up now.
		if(userName.length == 0)
		{
			subResponse.writeHead(404, { "Content-Type": "text/plain" });
			console.log("Attempt to create a subscription for a blank user. That's no good.");
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
			res.on('data', function (chunk) {
				dataReceived += chunk;
			});
			res.on('end', function () {
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
					console.log("Parsed query: ");
					console.dir(parsedQuery);

					if (parsedQuery.hasOwnProperty('notificationType')) {
						saveObject.notificationType = parsedQuery['notificationType'];
					} else {
						subResponse.writeHead(400, { "Content-Type": "text/plain" });
						console.log("Missing notification type.");
						subResponse.end("Missing notification type.");
						return;
					}
					if (parsedQuery.hasOwnProperty('deviceId')) {
						saveObject.deviceId = parsedQuery['deviceId'];
					} else {
						subResponse.writeHead(400, { "Content-Type": "text/plain" });
						console.log("Missing device id.");
						subResponse.end("Missing device id.");
						return;
					}
				}

				console.log("Subscribing with info:");
				console.dir(saveObject);

				var userDirectory = path.join(subscriptionDirectory, userName);

				if (!DirectoryExists(userDirectory)) {
					console.log("Directory " + userDirectory + " doesn't exist, creating.");
					fs.mkdirSync(userDirectory, 0777);
				}
				else {
					//Make sure the user has less than 5 devices, otherwise we'll replace the oldest one.
					//TODO: Replace the oldest one.
					//			subResponse.writeHead(400, { "Content-Type": "text/plain" });
					//			subResponse.end("Too many devices.");
					//			return;
				}

				console.log("Saving data to " + path.join(userDirectory, saveObject.deviceId));

				fs.writeFileSync(path.join(userDirectory, saveObject.deviceId), JSON.stringify(saveObject));
				console.log("Saved file!");
				subResponse.writeHead(200, { "Content-Type": "text/plain" });
				subResponse.end("Subscribed " + userName);
			});
		});
	}
	catch (ex) {
		console.log("Exception caught in subscription %s", ex);
		subResponse.writeHead(400, { "Content-Type": "text/plain" });
		subResponse.end("Unknown error.");
	}
}


//Remove a user
function RemoveRequest(response, parsedUrl, userName) {
	if (parsedUrl.hasOwnProperty('query')) {
		var parsedQuery = querystring.parse(parsedUrl.query);
		if (parsedQuery.hasOwnProperty('deviceId')) {

			var userDirectory = path.join(subscriptionDirectory, userName);

			if (DirectoryExists(userDirectory)) {
				var file = path.join(userDirectory, parsedQuery['deviceId']);
				path.exists(file, function (exists) {
					if (exists) {
						fs.unlinkSync(file);
						console.log('Request for removal of ' + userName + ' successful.');
					}
				});
			}
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

http.createServer(function (request, response) {
	var requestData = '';
	request.on('data', function (chunk) {
		requestData += chunk;
	});
	request.on('end', function () {
		var parsedUrl = url.parse(request.url);
		var splitPath = parsedUrl.pathname.split('/');

		console.log('Parsed URL: ');
		console.dir(parsedUrl);

		if (splitPath.length > 3) {
			console.log('more than two path variables were passed, bailing.');
			return;
		}

		switch (splitPath[1]) {
			case 'subscribe':
				SubscribeRequest(response, splitPath[2], parsedUrl, requestData);
				break;
			case 'remove':
				RemoveRequest(response, parsedUrl, splitPath[2]);
				break;
			default:
				response.writeHead(404, { "Content-Type": "text/plain" });
				response.end("404 Not Found\n");
				return;
		}
	});
}).listen(12243);

console.log("Server running at http://localhost:12243/");