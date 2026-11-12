param([switch]$Installer, [string]$NsisPath = 'makensis.exe', [ValidateSet('zh','en')][string]$Language = 'zh')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$payload = Join-Path $root 'build\app'
$release = Join-Path $root 'dist\updates\windows'
$appId = 'GetUpLockPublic'
$updatePath = 'updates/windows'
$languageOptions = @("/resource:$root\assets\feedback-wechat.png,feedback-wechat.png")
if ($Language -eq 'en') {
    $payload = Join-Path $root 'build\en\app'
    $release = Join-Path $release 'en'
    $appId = 'GetUpLockPublicEN'
    $updatePath += '/en'
    $languageOptions = @('/define:ENGLISH')
}
$vendor = Join-Path $root 'vendor\mediapipe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach ($folder in @($payload, "$payload\assets", "$payload\licenses")) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
}
function Confirm-Hash($path, $expected) {
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expected) {
        throw "Dependency checksum mismatch: $path"
    }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$engine = Join-Path $payload 'libmediapipe.dll'
$archive = [IO.Compression.ZipFile]::OpenRead("$vendor\libmediapipe.dll.zip")
try {
    $entry = $archive.GetEntry('libmediapipe.dll')
    if ($null -eq $entry -or $entry.Length -ne 62025728) { throw 'Unexpected MediaPipe archive contents' }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $engine, $true)
} finally {
    $archive.Dispose()
}
Confirm-Hash $engine '1808931f19961b8a9968bad0f719d1649048e87002dbec1ac644c9c017b2a22a'
Confirm-Hash "$root\assets\pose_landmarker_lite.task" '59929e1d1ee95287735ddd833b19cf4ac46d29bc7afddbbf6753c459690d574a'
Copy-Item -LiteralPath "$root\config.json","$root\src\GetUpLock.exe.config" -Destination $payload -Force
Copy-Item -LiteralPath "$root\assets\alert.wav","$root\assets\pose_landmarker_lite.task" -Destination "$payload\assets" -Force
foreach ($name in @('LICENSE','NOTICE')) {
    Copy-Item -LiteralPath "$vendor\$name" -Destination "$payload\licenses\MediaPipe-$name.txt" -Force
}
$sources = @(Get-ChildItem -LiteralPath "$root\src" -Filter '*.cs' -File |
    Where-Object { $Language -eq 'zh' -or $_.Name -ne 'FeedbackDialog.cs' } |
    Select-Object -ExpandProperty FullName)
$sources += "$root\locale\$Language\Strings.cs"
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll','/r:System.Net.Http.dll')
& $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:winexe @references @languageOptions "/win32icon:$root\assets\app.ico" "/win32manifest:$root\src\app.manifest" "/out:$payload\GetUpLock.exe" @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
foreach ($oldName in @('SquatLock.exe','SquatLock.exe.config')) {
    $oldOutput = Join-Path $payload $oldName
    if (Test-Path -LiteralPath $oldOutput -PathType Leaf) { Remove-Item -LiteralPath $oldOutput }
}
Write-Output "$payload\GetUpLock.exe"
if ($Installer) {
    $nsis = (Get-Command $NsisPath -CommandType Application).Source
    $program = Get-Content -LiteralPath "$root\src\Program.cs" -Raw
    $version = [regex]::Match($program, 'const string Version = "([0-9.]+)"').Groups[1].Value
    $build = [long][regex]::Match($program, 'const long BuildNumber = (\d+)').Groups[1].Value
    New-Item -ItemType Directory -Path $release -Force | Out-Null
    & $nsis /V2 "/DWORKSPACE=$root" "/DPAYLOAD=$payload" "/DRELEASE_DIR=$release" "/DAPP_VERSION=$version" "/DAPP_BUILD=$build" "/DEDITION=$Language" "$root\installer\Setup.nsi"
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    $file = Get-Item -LiteralPath "$release\$appId-Setup-$version-$build.exe"
    $manifest = [ordered]@{
        ok = $true; latest_version = $version; build = $build; notes = ''
        download_url = "https://ads.xjwisdomsoft.cn/$updatePath/$($file.Name)"
        size_bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant()
        in_app_update = 1
    }
    [IO.File]::WriteAllText("$release\release.json", ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Output $release
}
