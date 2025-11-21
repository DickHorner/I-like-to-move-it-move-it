#!/bin/bash

echo "Building ProgramMover - Single File Executable"
echo "=============================================="
echo ""

echo "Cleaning previous builds..."
rm -rf bin/Release
rm -rf obj

echo ""
echo "Building Release version..."
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true

echo ""
echo "Build complete!"
echo ""
echo "Output location: bin/Release/net7.0-windows/win-x64/publish/ProgramMover.exe"
echo ""
