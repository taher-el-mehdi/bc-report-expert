#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes Report Expert (self-contained win-x64) and builds release artifacts:
  portable ZIP, optional Inno Setup installer, and Microsoft Store MSIX package.

.PARAMETER SkipInstaller
  Skip Inno Setup when ISCC.exe is unavailable or not needed.

.PARAMETER SkipMsix
  Skip MSIX packaging.

.PARAMETER SkipSign
  Produce an unsigned .msix (Partner Center can still accept it for Store upload).

.PARAMETER PackageIdentityName
  MSIX Identity Name. Replace with Partner Center "Package/Identity name" before Store submission.

.PARAMETER Publisher
  MSIX Identity Publisher (certificate subject). Must match the signing cert for sideload.
  Replace with Partner Center "Publisher" value before Store submission.

.PARAMETER PublisherDisplayName
  Human-readable publisher shown in the Store / installer UI.

.EXAMPLE
  .\scripts\build-release.ps1
  .\scripts\build-release.ps1 -SkipInstaller
  .\scripts\build-release.ps1 -PackageIdentityName 'Contoso.ReportExpert' -Publisher 'CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX'
#>
param(
    [switch]$SkipInstaller,
    [switch]$SkipMsix,
    [switch]$SkipSign,
    [string]$PackageIdentityName = 'TaherElMehdi.ReportExpert',
    [string]$Publisher = 'CN=TAHER El Mehdi',
    [string]$PublisherDisplayName = 'TAHER El Mehdi'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$SdkBuildToolsVersion = '10.0.26100.1742'
$AppDisplayName = 'Report Expert'
$AppDescription = 'Desktop toolkit for RDLC report development — preview, Copilot edits, and Business Central report gallery.'

function Get-MsBuildProperty {
    param(
        [string[]]$CandidatePaths,
        [string]$PropertyName,
        [string]$Default = $null
    )

    foreach ($projectPath in $CandidatePaths) {
        if (-not (Test-Path $projectPath)) { continue }
        $match = Select-String -Path $projectPath -Pattern "<$PropertyName>([^<]+)</$PropertyName>" | Select-Object -First 1
        if ($match) {
            return $match.Matches[0].Groups[1].Value.Trim()
        }
    }

    if ($null -ne $Default) { return $Default }
    throw "Could not read <$PropertyName> from: $($CandidatePaths -join ', ')"
}

function ConvertTo-MsixVersion {
    param([string]$Version)
    $parts = @($Version.Split('.') | ForEach-Object { $_ })
    while ($parts.Count -lt 4) { $parts += '0' }
    if ($parts.Count -gt 4) { $parts = $parts[0..3] }
    return ($parts -join '.')
}

function Find-SdkTool {
    param([string]$ToolName)

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $kitsRoot) {
        $found = Get-ChildItem -Path $kitsRoot -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
            Where-Object { $_.DirectoryName -match '\\x64$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -products * -property installationPath 2>$null
        if ($vsPath) {
            $found = Get-ChildItem -Path $vsPath -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
                Where-Object { $_.DirectoryName -match '\\x64$' } |
                Select-Object -First 1
            if ($found) { return $found.FullName }
        }
    }

    return $null
}

function Install-SdkBuildTools {
    param([string]$CacheRoot, [string]$Version)

    $extractDir = Join-Path $CacheRoot "sdk-buildtools-$Version"
    $makeAppx = Join-Path $extractDir "bin\10.0.26100.0\x64\makeappx.exe"
    if (Test-Path $makeAppx) { return $extractDir }

    New-Item -ItemType Directory -Force -Path $CacheRoot | Out-Null
    $nupkg = Join-Path $CacheRoot "Microsoft.Windows.SDK.BuildTools.$Version.nupkg"
    $zip = Join-Path $CacheRoot "Microsoft.Windows.SDK.BuildTools.$Version.zip"
    $url = "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$Version/microsoft.windows.sdk.buildtools.$Version.nupkg"

    Write-Host "==> Downloading Windows SDK BuildTools $Version (MakeAppx)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $url -OutFile $nupkg
    Copy-Item $nupkg $zip -Force
    if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $extractDir -Force
    Remove-Item $zip -Force -ErrorAction SilentlyContinue

    if (-not (Test-Path $makeAppx)) {
        throw "MakeAppx.exe not found after extracting SDK BuildTools to $extractDir"
    }
    return $extractDir
}

function Resolve-MakeAppx {
    param([string]$ToolsCache)

    $existing = Find-SdkTool -ToolName 'makeappx.exe'
    if ($existing) { return $existing }

    $sdkDir = Install-SdkBuildTools -CacheRoot $ToolsCache -Version $SdkBuildToolsVersion
    return (Join-Path $sdkDir 'bin\10.0.26100.0\x64\makeappx.exe')
}

function Resolve-SignTool {
    param([string]$ToolsCache)

    $existing = Find-SdkTool -ToolName 'signtool.exe'
    if ($existing) { return $existing }

    $sdkDir = Join-Path $ToolsCache "sdk-buildtools-$SdkBuildToolsVersion"
    $candidate = Join-Path $sdkDir 'bin\10.0.26100.0\x64\signtool.exe'
    if (Test-Path $candidate) { return $candidate }

    $sdkDir = Install-SdkBuildTools -CacheRoot $ToolsCache -Version $SdkBuildToolsVersion
    return (Join-Path $sdkDir 'bin\10.0.26100.0\x64\signtool.exe')
}

function New-SquarePng {
    param(
        [System.Drawing.Image]$Source,
        [int]$Size,
        [string]$Destination
    )

    $side = [Math]::Min($Source.Width, $Source.Height)
    $srcX = [int](($Source.Width - $side) / 2)
    $srcY = [int](($Source.Height - $side) / 2)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $bmp.SetResolution(72, 72)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.DrawImage($Source, (New-Object System.Drawing.Rectangle 0, 0, $Size, $Size), $srcX, $srcY, $side, $side, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $g.Dispose()
    }

    $dir = Split-Path -Parent $Destination
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function New-WidePng {
    param(
        [System.Drawing.Image]$Source,
        [int]$Width,
        [int]$Height,
        [string]$Destination
    )

    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $bmp.SetResolution(72, 72)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        # Navy aligned with Report Expert hoodie branding
        $g.Clear([System.Drawing.Color]::FromArgb(255, 15, 23, 42))
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $scale = [Math]::Min($Width / [double]$Source.Width, $Height / [double]$Source.Height) * 0.85
        $drawW = [int]($Source.Width * $scale)
        $drawH = [int]($Source.Height * $scale)
        $drawX = [int](($Width - $drawW) / 2)
        $drawY = [int](($Height - $drawH) / 2)
        $g.DrawImage($Source, $drawX, $drawY, $drawW, $drawH)
    }
    finally {
        $g.Dispose()
    }

    $dir = Split-Path -Parent $Destination
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function New-MsixAssets {
    param(
        [string]$LogoPath,
        [string]$AssetsDir
    )

    Add-Type -AssemblyName System.Drawing
    $source = [System.Drawing.Image]::FromFile((Resolve-Path $LogoPath))
    try {
        New-SquarePng -Source $source -Size 50  -Destination (Join-Path $AssetsDir 'StoreLogo.png')
        New-SquarePng -Source $source -Size 44  -Destination (Join-Path $AssetsDir 'Square44x44Logo.png')
        New-SquarePng -Source $source -Size 71  -Destination (Join-Path $AssetsDir 'Square71x71Logo.png')
        New-SquarePng -Source $source -Size 150 -Destination (Join-Path $AssetsDir 'Square150x150Logo.png')
        New-SquarePng -Source $source -Size 310 -Destination (Join-Path $AssetsDir 'Square310x310Logo.png')
        New-WidePng   -Source $source -Width 310 -Height 150 -Destination (Join-Path $AssetsDir 'Wide310x150Logo.png')

        # Unplated taskbar / Alt+Tab variants
        Copy-Item (Join-Path $AssetsDir 'Square44x44Logo.png') (Join-Path $AssetsDir 'Square44x44Logo.targetsize-44.png') -Force
        Copy-Item (Join-Path $AssetsDir 'Square44x44Logo.png') (Join-Path $AssetsDir 'Square44x44Logo.targetsize-44_altform-unplated.png') -Force
    }
    finally {
        $source.Dispose()
    }
}

function New-MsixManifest {
    param(
        [string]$TemplatePath,
        [string]$DestinationPath,
        [string]$IdentityName,
        [string]$Publisher,
        [string]$PublisherDisplayName,
        [string]$MsixVersion,
        [string]$DisplayName,
        [string]$Description
    )

    [xml]$manifest = Get-Content -Path $TemplatePath -Raw
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace('def', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $ns.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')

    $identity = $manifest.Package.Identity
    $identity.Name = $IdentityName
    $identity.Publisher = $Publisher
    $identity.Version = $MsixVersion
    $identity.ProcessorArchitecture = 'x64'

    $manifest.Package.Properties.DisplayName = $DisplayName
    $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
    $manifest.Package.Properties.Description = $Description

    $app = $manifest.Package.Applications.Application
    $visual = $app.VisualElements
    if (-not $visual) {
        $visual = $app.SelectSingleNode('uap:VisualElements', $ns)
    }
    if ($visual) {
        $visual.DisplayName = $DisplayName
        $visual.Description = $Description
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($DestinationPath, $manifest.OuterXml, $utf8NoBom)
}

function New-SelfSignedMsixCertificate {
    param(
        [string]$Subject,
        [string]$PfxPath,
        [securestring]$Password
    )

    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Subject `
        -KeyUsage DigitalSignature `
        -FriendlyName 'Report Expert MSIX Dev' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @(
            '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
            '2.5.29.19={text}'
        )

    Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $Password | Out-Null
    Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue
    return $PfxPath
}

# --- Main --------------------------------------------------------------------

$csproj = Join-Path $Root 'src\ReportExpert.App\ReportExpert.App.csproj'
$directoryBuildProps = Join-Path $Root 'Directory.Build.props'
if (-not (Test-Path $csproj)) {
    throw "Project file not found: $csproj"
}

$versionSources = @($csproj, $directoryBuildProps)
$Version = Get-MsBuildProperty -CandidatePaths $versionSources -PropertyName 'Version'
$DisplayVersion = Get-MsBuildProperty -CandidatePaths $versionSources -PropertyName 'VersionDisplay' -Default ($Version -replace '\.0$', '')
$Codename = Get-MsBuildProperty -CandidatePaths $versionSources -PropertyName 'VersionCodename' -Default ''
$ReleaseLabel = if ($Codename) { "v$DisplayVersion-$Codename" } else { "v$DisplayVersion" }
$MsixVersion = ConvertTo-MsixVersion -Version $Version
$InstallerFileName = "ReportExpert-Setup-$ReleaseLabel.exe"
$MsixFileName = "ReportExpert-$ReleaseLabel-x64.msix"

$publishDir = Join-Path $Root 'artifacts\publish\win-x64'
$installerDir = Join-Path $Root 'artifacts\installer'
$msixDir = Join-Path $Root 'artifacts\msix'
$msixLayout = Join-Path $msixDir 'layout'
$msixPath = Join-Path $msixDir $MsixFileName
$zipPath = Join-Path $Root "artifacts\ReportExpert-$ReleaseLabel-win-x64.zip"
$toolsCache = Join-Path $Root 'artifacts\tools'
$manifestTemplate = Join-Path $Root 'installer\msix\AppxManifest.xml'
$logoSource = Join-Path $Root 'Assets\app-logo.png'
# Prefer packaging Assets copy; fall back to repo-root logo.png
if (-not (Test-Path $logoSource)) {
    $logoSource = Join-Path $Root 'logo.png'
}

Write-Host "==> Report Expert release build $ReleaseLabel (assembly $Version)" -ForegroundColor Cyan

if (Test-Path (Join-Path $Root 'artifacts')) {
    Get-ChildItem (Join-Path $Root 'artifacts') -Force |
        Where-Object { $_.Name -ne 'tools' } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $publishDir, $installerDir, $msixDir | Out-Null

Write-Host '==> dotnet publish (self-contained win-x64)...' -ForegroundColor Cyan
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$versionStamp = @(
    "Report Expert $ReleaseLabel"
    "Assembly version: $Version"
) -join [Environment]::NewLine
Set-Content -Path (Join-Path $publishDir 'VERSION.txt') -Value $versionStamp -Encoding utf8

Write-Host '==> Creating portable ZIP...' -ForegroundColor Cyan
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# Stage a copy so Compress-Archive does not race with AV / leftover file locks on publish output.
$zipStage = Join-Path $Root 'artifacts\zip-stage'
if (Test-Path $zipStage) { Remove-Item $zipStage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $zipStage | Out-Null
Copy-Item -Path (Join-Path $publishDir '*') -Destination $zipStage -Recurse -Force
Compress-Archive -Path (Join-Path $zipStage '*') -DestinationPath $zipPath
Remove-Item $zipStage -Recurse -Force -ErrorAction SilentlyContinue

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
) | Where-Object { $_ -and (Test-Path $_) }

$iscc = $isccCandidates | Select-Object -First 1
$iss = Join-Path $Root 'installer\ReportExpert.iss'

if ($SkipInstaller) {
    Write-Host '==> Skipping Inno Setup (-SkipInstaller).' -ForegroundColor Yellow
}
elseif (-not $iscc) {
    Write-Host '==> Inno Setup not found. Install from https://jrsoftware.org/isinfo.php' -ForegroundColor Yellow
    Write-Host '    Publish output and ZIP are ready in artifacts\' -ForegroundColor Yellow
}
else {
    Write-Host "==> Building installer with $iscc ..." -ForegroundColor Cyan
    & $iscc $iss "/DMyAppVersion=$Version" "/DMyAppDisplayVersion=$DisplayVersion" "/DMyAppCodename=$Codename"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($SkipMsix) {
    Write-Host '==> Skipping MSIX (-SkipMsix).' -ForegroundColor Yellow
}
else {
    if (-not (Test-Path $manifestTemplate)) {
        throw "MSIX manifest template not found: $manifestTemplate"
    }
    if (-not (Test-Path $logoSource)) {
        throw "Logo not found: $logoSource"
    }

    Write-Host '==> Preparing MSIX package layout...' -ForegroundColor Cyan
    if (Test-Path $msixLayout) { Remove-Item $msixLayout -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $msixLayout | Out-Null

    Copy-Item -Path (Join-Path $publishDir '*') -Destination $msixLayout -Recurse -Force
    Get-ChildItem -Path $msixLayout -Recurse -Filter *.pdb -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    $assetsDir = Join-Path $msixLayout 'Assets'
    New-MsixAssets -LogoPath $logoSource -AssetsDir $assetsDir

    $layoutManifest = Join-Path $msixLayout 'AppxManifest.xml'
    New-MsixManifest `
        -TemplatePath $manifestTemplate `
        -DestinationPath $layoutManifest `
        -IdentityName $PackageIdentityName `
        -Publisher $Publisher `
        -PublisherDisplayName $PublisherDisplayName `
        -MsixVersion $MsixVersion `
        -DisplayName $AppDisplayName `
        -Description $AppDescription

    $makeAppx = Resolve-MakeAppx -ToolsCache $toolsCache
    Write-Host "==> Packaging MSIX with $makeAppx ..." -ForegroundColor Cyan
    if (Test-Path $msixPath) { Remove-Item $msixPath -Force }
    & $makeAppx pack /d $msixLayout /p $msixPath /o
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if ($SkipSign) {
        Write-Host '==> Leaving MSIX unsigned (-SkipSign). Partner Center will re-sign Store submissions.' -ForegroundColor Yellow
    }
    else {
        $signTool = Resolve-SignTool -ToolsCache $toolsCache
        $pfxPath = Join-Path $msixDir 'msix-dev.pfx'
        $plainPassword = [guid]::NewGuid().ToString('N')
        $securePassword = ConvertTo-SecureString -String $plainPassword -Force -AsPlainText

        Write-Host '==> Signing MSIX with a local development certificate...' -ForegroundColor Cyan
        New-SelfSignedMsixCertificate -Subject $Publisher -PfxPath $pfxPath -Password $securePassword | Out-Null
        & $signTool sign /fd SHA256 /a /f $pfxPath /p $plainPassword $msixPath
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        Write-Host '    Dev cert PFX saved at artifacts\msix\msix-dev.pfx (local sideload only).' -ForegroundColor DarkGray
        Write-Host '    Microsoft Store re-signs packages during certification.' -ForegroundColor DarkGray
    }
}

Write-Host ''
Write-Host 'Release artifacts:' -ForegroundColor Green
Write-Host "  ZIP:       $zipPath"
if (Test-Path (Join-Path $installerDir $InstallerFileName)) {
    Write-Host "  Installer: $(Join-Path $installerDir $InstallerFileName)"
}
if (Test-Path $msixPath) {
    Write-Host "  MSIX:      $msixPath"
}
Write-Host ''
Write-Host 'Microsoft Store notes:' -ForegroundColor Green
Write-Host "  1. Reserve the app name in Partner Center."
Write-Host "  2. Copy Package/Identity name + Publisher into this script (or AppxManifest.xml)."
Write-Host "  3. Rebuild, then upload $MsixFileName under Packages."
Write-Host "  4. runFullTrust requires Store declaration/approval for full-trust desktop apps."
Write-Host ''
Write-Host 'GitHub Releases:' -ForegroundColor Green
Write-Host "  1. $InstallerFileName  (primary download)"
Write-Host "  2. ReportExpert-$ReleaseLabel-win-x64.zip (portable)"
Write-Host "  3. $MsixFileName (Microsoft Store / sideload)"
