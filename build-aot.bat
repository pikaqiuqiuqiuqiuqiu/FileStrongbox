@echo off
set "PATH=%PATH%;C:\Program Files (x86)\Microsoft Visual Studio\Installer"
call "E:\Soft\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
cd /d "E:\Files\52pj\projects\FileStrongbox"
dotnet publish -c Release -r win-x64 --self-contained true
pause
