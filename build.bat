@echo off
echo Building ProgramMover - Single File Executable (.NET 8)
echo ======================================================
echo.

echo Cleaning previous builds...
rmdir /s /q bin\Release 2>nul
rmdir /s /q obj 2>nul

echo.
echo Building Release version (explicit project)...
dotnet restore ProgramMover.csproj || goto :error
dotnet publish ProgramMover.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true || goto :error

echo.
echo Build complete!
echo.
echo Output location: bin\Release\net8.0-windows\win-x64\publish\ProgramMover.exe
echo.

echo.
echo Done.
goto :eof

:error
echo Build failed. See errors above.
exit /b 1
