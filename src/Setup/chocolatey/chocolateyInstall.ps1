$productName = "ServiceControl"
$url = "https://github.com/Particular/$productName/releases/download/$env:chocolateyPackageVersion/Particular.$productName-$env:chocolateyPackageVersion.exe"
Install-ChocolateyPackage $productName 'exe' '/quiet' $url