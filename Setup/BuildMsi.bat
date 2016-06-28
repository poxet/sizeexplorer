for /f "delims=" %%i in ('"C:\Program Files (x86)\Windows Kits\8.1\bin\x64\uuidgen.exe"') do set guid=%%i
"C:\Program Files (x86)\WiX Toolset v3.10\bin\candle.exe" SizeExplorer.wsx -dEnvironment="%2" -dVersion="%1" -dId="%guid%"
"C:\Program Files (x86)\WiX Toolset v3.10\bin\light.exe" -ext WixUIExtension -cultures:en-us SizeExplorer.wixobj