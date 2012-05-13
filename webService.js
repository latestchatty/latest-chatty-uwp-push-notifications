var sys = require("util"),
	 http = require("http"),
	url = require("url"),
	path = require("path"),
	querystring = require("querystring"),
	fs = require("fs");

var subscriptionDirectory = path.join(process.cwd(), 'subscribedUsers');

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
function SubscribeRequest(response, userName, parsedUrl, requestData) {
	console.log("Subscribe Called.");

	try {
		var saveObject = {
			userName: userName,
			dateCreated: new Date(),
			replyCount: 0,
			notificationUri: requestData,
			deviceId: '',
			notificationType: 1 //1 = Tile only, 2 = Tile and Toast
		};

		if (parsedUrl.hasOwnProperty('query')) {
			var parsedQuery = querystring.parse(parsedUrl.query);
			console.log("Parsed query: ");
			console.dir(parsedQuery);
			if (parsedQuery.hasOwnProperty('currentCount')) {
				saveObject.replyCount = parsedQuery['currentCount'];
			} else {
				response.writeHead(400, { "Content-Type": "text/plain" });
				console.log("Missing count.");
				response.end("Missing count.");
				return;
			}
			if (parsedQuery.hasOwnProperty('notificationType')) {
				saveObject.notificationType = parsedQuery['notificationType'];
			} else {
				response.writeHead(400, { "Content-Type": "text/plain" });
				console.log("Missing notification type.");
				response.end("Missing notification type.");
				return;
			}
			if (parsedQuery.hasOwnProperty('deviceId')) {
				saveObject.deviceId = parsedQuery['deviceId'];
			} else {
				response.writeHead(400, { "Content-Type": "text/plain" });
				console.log("Missing device id.");
				response.end("Missing device id.");
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
			//			response.writeHead(400, { "Content-Type": "text/plain" });
			//			response.end("Too many devices.");
			//			return;
		}

		console.log("Saving data to " + path.join(userDirectory, saveObject.deviceId));

		fs.writeFileSync(path.join(userDirectory, saveObject.deviceId), JSON.stringify(saveObject));
		console.log("Saved file!");
		response.writeHead(200, { "Content-Type": "text/plain" });
		response.end("Subscribed " + userName);
	}
	catch (ex) {
		console.log("Exception caught in subscription %s", ex);
		response.writeHead(400, { "Content-Type": "text/plain" });
		response.end("Unknown error.");
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
		//Should probably sanatize this.
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