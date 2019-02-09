docker build -t boarder2/latest-chatty-uwp-push-notifications:dev -f Dockerfile.dev .
rem docker run -it --rm -p 4000:4000 --mount source="%cd%database",target=/database --mount source="%cd%\log",target=/log --mount source="%cd%\src",target=/dotnetapp boarder2/latest-chatty-uwp-push-notifications:dev
docker run -it --rm -p 4000:4000 --name latest-chatty-uwp-push-notifications-dev -v "%cd%\database":/database -v "%cd%\log":/log -v "%cd%\src":/dotnetapp boarder2/latest-chatty-uwp-push-notifications:dev