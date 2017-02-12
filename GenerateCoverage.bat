%HOMEPATH%\.nuget\packages\OpenCover\4.6.519\tools\OpenCover.Console.exe -target:"C:\Program Files\dotnet\dotnet.exe" -targetargs:"test ""test\SNPN.Test""" -filter:"+[*]* -[xunit*]*" -output:coverage.xml -oldStyle -register:user
%HOMEPATH%\.nuget\packages\ReportGenerator\2.5.2\tools\ReportGenerator.exe -reports:coverage.xml -targetdir:"coverage-report"
