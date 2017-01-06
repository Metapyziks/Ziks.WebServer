@echo off

pushd %~dp0\Ziks.WebServer

nuget pack Ziks.WebServer.csproj -OutputDirectory "..\packages" -Properties Configuration=Release

popd
