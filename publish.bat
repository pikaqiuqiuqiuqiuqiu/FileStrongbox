@echo off
chcp 65001 >nul
set "PATH=%PATH%;C:\Program Files (x86)\Microsoft Visual Studio\Installer"

echo Setting up Visual Studio environment...
call "E:\Soft\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"

echo.
echo Building AOT single file...
cd /d "E:\Files\52pj\projects\FileStrongbox"
dotnet publish -c Release -r win-x64 --self-contained true -o publish

echo.
echo Done! Check the publish folder.
dir publish\*.exe
