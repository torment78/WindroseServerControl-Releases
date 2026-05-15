param(
    [switch]$UploadToGitHub
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$InstallerDir = $PSScriptRoot
$VersionFile = Join-Path $InstallerDir "version.txt"
$IssFile = Join-Path $InstallerDir "WindroseServerControl.iss"

$ProjectFile = Join-Path $Root "Elka_windrose_server_control.csproj"
$PublishDir = Join-Path $Root "publish\win-x64"
$OutputDir = Join-Path $Root "Installer\Output"
$PortableZipFile = Join-Path $OutputDir "WindroseServerControl_Portable_v$NewVersion.zip"

$Repo = "torment78/WindroseServerControl-Releases"

function Increment-Version($versionText) {
    $v = [Version]$versionText
    return "$($v.Major).$($v.Minor).$($v.Build).$($v.Revision + 1)"
}

if (!(Test-Path $VersionFile)) {
    "1.0.0.0" | Set-Content $VersionFile
}

$OldVersion = (Get-Content $VersionFile -Raw).Trim()
$NewVersion = Increment-Version $OldVersion

$NewVersion | Set-Content $VersionFile
$PortableZipFile = Join-Path $OutputDir "WindroseServerControl_Portable_v$NewVersion.zip"

Write-Host "Version: $OldVersion -> $NewVersion"

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "Publishing app..."

dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $PublishDir `
    /p:Version=$NewVersion `
    /p:AssemblyVersion=$NewVersion `
    /p:FileVersion=$NewVersion

Write-Host "Building Inno installer..."

$InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"

if (!(Test-Path $InnoCompiler)) {
    throw "Inno Setup compiler not found: $InnoCompiler"
}

$env:WINDROSE_APP_VERSION = $NewVersion
$env:WINDROSE_PUBLISH_DIR = $PublishDir
$env:WINDROSE_OUTPUT_DIR = $OutputDir

& $InnoCompiler $IssFile

$InstallerFile = Join-Path $OutputDir "WindroseServerControl_Setup_v$NewVersion.exe"

if (!(Test-Path $InstallerFile)) {
    throw "Installer was not created: $InstallerFile"
}

Write-Host "Installer created:"
Write-Host $InstallerFile
Write-Host "Creating portable ZIP..."

if (Test-Path $PortableZipFile) {
    Remove-Item $PortableZipFile -Force
}

Compress-Archive `
    -Path (Join-Path $PublishDir "*") `
    -DestinationPath $PortableZipFile `
    -Force

Write-Host "Portable ZIP created:"
Write-Host $PortableZipFile

if ($UploadToGitHub) {
    

    Write-Host "Uploading to GitHub release..."

   & "C:\Program Files\GitHub CLI\gh.exe" release create "v$NewVersion" $InstallerFile $PortableZipFile `
        --repo $Repo `
        --title "Windrose Server Control v$NewVersion" `
        --notes "Installer release v$NewVersion"

    Write-Host "GitHub release uploaded."
}
else {
    Write-Host "GitHub upload skipped. Use -UploadToGitHub to publish."
}