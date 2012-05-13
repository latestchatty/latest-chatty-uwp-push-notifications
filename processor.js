var sys = require("util"),
	 http = require("http"),
	 url = require("url"),
	path = require("path"),
	fs = require("fs"),
	libxmljs = require("libxmljs");

var subscriptionDirectory = '/home/' + path.join('wzutz', 'Dropbox', 'Shack Node', 'subscribedUsers');

function SendWP7ToastNotification(author, preview, pushUri) {
	//console.log('**Sending WP7 push notification to ' + pushUri + '\n  Author: ' + author + '\n  Preview: ' + preview);

	var parsedUri = url.parse(pushUri);

	var toastData = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<wp:Notification xmlns:wp=\"WPNotification\">" +
           "<wp:Toast>" +
              "<wp:Text1>" + author + "</wp:Text1>" +
              "<wp:Text2>" + preview.substr(0, 40) + "</wp:Text2>" +
           "</wp:Toast>" +
        "</wp:Notification>";

	console.log('**Sending toast notification\n' + toastData);

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
				console.log('Sending push failed.');
				console.log('Code: ' + res.statusCode);
				console.log('Notification Status: ' + notificationStatus);
				console.log('Device Connection Status: ' + deviceConnectionStatus);
				console.log('Subscription Status: ' + subscriptionStatus);
				console.log('Body: ' + responseBody);
			}
			else {
				console.log('Toast notification sent successfully!');
			}
		});
	});

	request.on('error', function (e) {
		console.log('Request Failed: ' + e.message);
	});

	// write data to request body
	request.write(toastData);
	request.end();
}

function SendWP7TileNotification(count, author, preview, pushUri) {
	//console.log('**Sending WP7 push notification to ' + pushUri + '\n  Author: ' + author + '\n  Preview: ' + preview);

	var parsedUri = url.parse(pushUri);

	var tileMessage = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
		"<wp:Notification xmlns:wp=\"WPNotification\">" +
			 "<wp:Tile>" +
				  "<wp:Count>" + count + "</wp:Count>" +
				  "<wp:BackTitle>" + author + "</wp:BackTitle>" +
				  "<wp:BackContent>" + preview + "</wp:BackContent>" +
			 "</wp:Tile> " +
		"</wp:Notification>";

	console.log('**Sending tile notification\n' + tileMessage);

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
				console.log('Sending push failed.');
				console.log('Code: ' + res.statusCode);
				console.log('Notification Status: ' + notificationStatus);
				console.log('Device Connection Status: ' + deviceConnectionStatus);
				console.log('Subscription Status: ' + subscriptionStatus);
				console.log('Body: ' + responseBody);
			}
			else {
				console.log('Tile notification sent successfully!');
			}
		});
	});

	request.on('error', function (e) {
		console.log('Request Failed: ' + e.message);
	});

	// write data to request body
	request.write(tileMessage);
	request.end();
}

function ProcessUser(userInfo, requestOptions) {
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
			var newReplyCount = parseInt(totalResults) - parseInt(userInfo.replyCount);
			if (newReplyCount > 0) {
				console.log('Previous count for %s was %s current count is %s, we got new stuff!', userInfo.userName, userInfo.replyCount, totalResults);

				var latestResult = xmlDoc.get('//result');
				if (latestResult != null) {
					var author = latestResult.attr('author').value();
					var body = latestResult.get('body').text();

					if (userInfo.hasOwnProperty('notificationUri')) {
						if (userInfo.notificationType = 2) {
							SendWP7ToastNotification(author, body, userInfo.notificationUri);
						}
						SendWP7TileNotification(newReplyCount, author, body, userInfo.notificationUri);
					}
					else {
						console.log('Would send push notification of\n  Author: ' + author + '\n  Preview: ' + body);
					}
				}

				//Since we got new stuff, it's time to update the current count.
				userInfo.replyCount = totalResults;
				var fileNameToSave = path.join(subscriptionDirectory, userInfo.userName, userInfo.deviceId);
				fs.writeFile(fileNameToSave, JSON.stringify(userInfo), function (err) {
					if (err) { console.log("Error saving file %s %s", fileNameToSave, err); }
					else { console.log("Saved updated file %s for username %s!", fileNameToSave, userInfo.userName); }
				});
			}
			else {
				console.log('No new replies for %s, previous count was %s current count is %s', userInfo.userName, userInfo.replyCount, totalResults);
			}
		});
	});
}

function GetDirectories(dir) {
	var directories = new Array();
	var items = fs.readdirSync(dir);
	for (var iItem = 0; iItem < items.length; iItem++) {
		var stats = fs.lstatSync(dir);
		if (stats.isDirectory()) {
			directories.push(items[iItem]);
		}
	}

	console.log("Directories to process: ");
	console.dir(directories);
	return directories;
}

function ProcessDirectory(dir) {
	console.log("Processing directory " + dir);
	fs.readdir(dir, function (err, files) {
		for (var iFile = 0; iFile < files.length; iFile++) {
			console.log('Processing file ' + files[iFile] + ' in ' + dir);
			var fileData = fs.readFileSync(path.join(dir, files[iFile]), 'utf8');

			var userData = JSON.parse(fileData);

			var siteUrl = url.parse('http://shackapi.stonedonkey.com/Search/?ParentAuthor=' + userData.userName);

			var requestOptions = {
				host: siteUrl.host,
				port: 80,
				path: siteUrl.path
			};

			ProcessUser(userData, requestOptions);
		}
	});
}

var directories = GetDirectories(subscriptionDirectory);
for (var iDir = 0; iDir < directories.length; iDir++) {
	var dir = path.join(subscriptionDirectory, directories[iDir]);
	ProcessDirectory(dir);
}