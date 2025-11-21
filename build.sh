#!/bin/bash

echo "Building ProgramMover - Single File Executable (.NET 8)"
echo "======================================================="
echo ""

echo "Cleaning previous builds..."
rm -rf bin/Release
rm -rf obj

echo ""
echo "Building Release version (explicit project)..."
dotnet restore ProgramMover.csproj || { echo "restore failed"; exit 1; }
dotnet publish ProgramMover.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true || { echo "publish failed"; exit 1; }

echo ""
echo "Build complete!"
echo ""
echo "Output location: bin/Release/net8.0-windows/win-x64/publish/ProgramMover.exe"
echo ""
