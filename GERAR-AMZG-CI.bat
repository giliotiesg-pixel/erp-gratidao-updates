@echo off
setlocal EnableExtensions
cd /d "%~dp0"
if exist publish rmdir /s /q publish
if exist publish-maintenance rmdir /s /q publish-maintenance
if exist installer\Output rmdir /s /q installer\Output
dotnet restore AMZG.App\AMZG.App.csproj || exit /b 1
dotnet restore AMZG.Maintenance\AMZG.Maintenance.csproj || exit /b 1
dotnet publish AMZG.App\AMZG.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish || exit /b 1
dotnet publish AMZG.Maintenance\AMZG.Maintenance.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-maintenance || exit /b 1
copy /y publish-maintenance\AMZG.Maintenance.exe publish\AMZG.Maintenance.exe >nul || exit /b 1
for /r publish %%F in (*.db *.sqlite *.sqlite3) do exit /b 2
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\AMZG.iss || "C:\Program Files\Inno Setup 6\ISCC.exe" installer\AMZG.iss || exit /b 1
if not exist installer\Output\AMZG-Setup.exe exit /b 3
