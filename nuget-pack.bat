@echo off

set RELEASENOTES="None specified."
echo %RELEASENOTES%

if not %1=="" set RELEASENOTES=%1
echo %RELEASENOTES%


pushd %~dp0\Ziks.WebServer

nuget pack Ziks.WebServer.csproj -OutputDirectory "..\packages" -Properties Configuration=Release;releaseNotes=%RELEASENOTES%

popd
