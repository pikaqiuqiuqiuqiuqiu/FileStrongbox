@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul 2>&1

echo Adding vswhere to PATH...
set "PATH=%PATH%;C:\Program Files (x86)\Microsoft Visual Studio\Installer"

echo Setting up VC environment...
call "E:\Soft\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1

echo Current PATH includes vswhere:
where vswhere.exe

echo.
echo Starting AOT publish...
cd /d "E:\Files\52pj\projects\FileStrongbox"

dotnet publish FileStrongbox.csproj -c Release -r win-x64 --self-contained true -o "E:\Files\52pj\projects\FileStrongbox\publish"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Removing debug symbols...
    del /q "E:\Files\52pj\projects\FileStrongbox\publish\*.pdb" 2>nul

    echo.
    echo ========================================
    echo SUCCESS! Output files:
    dir /b "E:\Files\52pj\projects\FileStrongbox\publish\"
    echo ========================================
) else (
    echo.
    echo Build failed with error code %ERRORLEVEL%
)
