#!/bin/bash
APP_NAME="/Users/mcarr/RiderProjects/OConfigurator/output/OConfig.app"
PUBLISH_OUTPUT_DIRECTORY="/Users/mcarr/RiderProjects/OConfigurator/bin/Release/net8.0/osx-arm64/publish/."
# PUBLISH_OUTPUT_DIRECTORY should point to the output directory of your dotnet publish command.
# One example is /path/to/your/csproj/bin/Release/netcoreapp3.1/osx-x64/publish/.
# If you want to change output directories, add `--output /my/directory/path` to your `dotnet publish` command.
INFO_PLIST="/Users/mcarr/RiderProjects/OConfigurator/Info.plist"
ICON_FILE="/Users/mcarr/RiderProjects/OConfigurator/Assets/DriveIcons.icns"

if [ -d "$APP_NAME" ]
then
    rm -rf "$APP_NAME"
fi

mkdir -p "$APP_NAME"

mkdir "$APP_NAME/Contents"
mkdir "$APP_NAME/Contents/MacOS"
mkdir "$APP_NAME/Contents/Resources"

echo "copying plist"
cp "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"

echo "copying icon"
echo 
cp "$ICON_FILE" "$APP_NAME/Contents/Resources/"

echo "copying publish dir"
cp -a "$PUBLISH_OUTPUT_DIRECTORY" "$APP_NAME/Contents/MacOS"


chmod +x $APP_NAME