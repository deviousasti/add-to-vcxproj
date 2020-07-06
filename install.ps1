dotnet pack -c Release -o nupkg
dotnet tool install --add-source .\nupkg -g add-to-vcxproj