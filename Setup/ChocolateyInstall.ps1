$packageName = 'windirstat'
$fileType = 'msi'
$url = 'http://www.thargelion.net/Resources/#Environment#/SizeExplorer.#Version#.msi'
$silentArgs = '/q'

Install-ChocolateyPackage $packageName $fileType "$silentArgs" "$url"