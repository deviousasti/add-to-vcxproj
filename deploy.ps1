dotnet pack -c Release -o nupkg

$package = Get-ChildItem "nupkg/*.nupkg" | Select-Object -First 1
if($package -ne $null)
{
    dotnet nuget push $package.FullName --source "github"
}
