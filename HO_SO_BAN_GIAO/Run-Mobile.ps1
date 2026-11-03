param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5180/api',
    [string]$Device = 'windows',
    [string]$MeterOcrUrl = 'http://127.0.0.1:5000/api/ocr-dong-ho',
    [string]$FirebaseCppSdkDir = 'C:\dev\firebase-cpp\firebase_cpp_sdk_windows'
)
$ErrorActionPreference = 'Stop'
$api = [uri]$ApiBaseUrl
if (!$api.IsAbsoluteUri -or $api.Scheme -notin @('http','https') -or
    $api.AbsolutePath.TrimEnd('/') -ne '/api') {
    throw 'ApiBaseUrl must be http(s)://HOST:PORT/api'
}
$flutterCommand = Get-Command flutter -ErrorAction SilentlyContinue
$flutterExe = if ($flutterCommand) { $flutterCommand.Source } elseif (Test-Path 'C:\dev\flutter\bin\flutter.bat') {
    'C:\dev\flutter\bin\flutter.bat'
} else { throw 'Flutter is not installed or not in PATH.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$shortAlias = Get-Item -LiteralPath 'C:\dev\qlpt-integration' -ErrorAction SilentlyContinue
if ($shortAlias -and $shortAlias.LinkType -eq 'Junction' -and
    @($shortAlias.Target) -contains $projectRoot) {
    $projectRoot = $shortAlias.FullName
}
Push-Location (Join-Path $projectRoot 'mobileui')
$previousFirebase = $env:FIREBASE_CPP_SDK_DIR
try {
    if ($Device -eq 'windows' -and (Test-Path (Join-Path $FirebaseCppSdkDir 'include\firebase\version.h'))) {
        $env:FIREBASE_CPP_SDK_DIR = $FirebaseCppSdkDir
    }
    & $flutterExe pub get
    if ($LASTEXITCODE -ne 0) { throw 'flutter pub get failed' }
    & $flutterExe run -d $Device "--dart-define=API_BASE_URL=$($ApiBaseUrl.TrimEnd('/'))" "--dart-define=METER_OCR_URL=$MeterOcrUrl"
    if ($LASTEXITCODE -ne 0) { throw "Flutter exited with code $LASTEXITCODE" }
} finally {
    $env:FIREBASE_CPP_SDK_DIR = $previousFirebase
    Pop-Location
}
