@echo off
echo Building ProgramMover - Single File Executable
echo =============================================
echo.

echo Cleaning previous builds...
rmdir /s /q bin\Release 2>nul
rmdir /s /q obj 2>nul

echo.
echo Building Release version...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true

echo.
echo Build complete!
echo.
echo Output location: bin\Release\net7.0-windows\win-x64\publish\ProgramMover.exe
echo.

pause
