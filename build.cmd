@echo off
setlocal
echo [CLEANING...]
if exist Release rmdir /s /q Release
if exist obj rmdir /s /q obj
if exist bin rmdir /s /q bin

echo [BUILDING TAG EDITOR PRO...]
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./Release

if exist icon.ico (
    echo [ICON FOUND] Application is branded.
)

echo.
echo BUILD SUCCESSFUL! YOUR APP IS IN THE 'Release' FOLDER.
pause