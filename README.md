# Shacknews Push Notifications
[![Build status](https://boarder2.visualstudio.com/Latest%20Chatty%20UWP%20Push%20Notifications/_apis/build/status/Latest%20Chatty%20UWP%20Push%20Notifications-ASP.NET%20Core-CI)](https://boarder2.visualstudio.com/Latest%20Chatty%20UWP%20Push%20Notifications/_build/latest?definitionId=10)

Push notification service for [Latest Chatty UWP](https://github.com/latestchatty/latest-chatty-uwp)

Also provides live tile support for the app.

Makes use of the [WinChatty v2 API](https://github.com/electroly/winchatty-server).

Documentatation for the WinChatty v2 API is available [here](http://winchatty.com/v2/readme).

How to build
------
 - Clone the repo
 - Ensure [.NET Core](https://www.microsoft.com/net/core) 2.1 or later is installed and in your path.
 - `dotnet restore`
 - `dotnet build`