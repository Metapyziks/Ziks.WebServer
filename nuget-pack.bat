@echo off

set RELEASENOTES="None specified."
if not %1=="" set RELEASENOTES=%1

pushd %~dp0\Ziks.WebServer

nuget pack Ziks.WebServer.csproj -OutputDirectory "..\packages" -Properties Configuration=Release;releaseNotes=%RELEASENOTES%

popd
