param(
    [string]$PackageRoot = '',
    [string]$TargetRoot = '',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Get-DefaultTargetRoot {
    $userProfile = $env:USERPROFILE
    if ([string]::IsNullOrWhiteSpace($userProfile)) {
        $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    }

    return Join-Path $userProfile '.lostcyberhamster\android-dev-signing'
}

function Resolve-ConfiguredPath {
    param(
        [string]$Path,
        [string]$BaseDirectory
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BaseDirectory $Path))
}

function Get-KeytoolPath {
    param([string]$UnityExePath)

    if (-not [string]::IsNullOrWhiteSpace($UnityExePath) -and (Test-Path -LiteralPath $UnityExePath)) {
        $unityRoot = Split-Path -Parent (Split-Path -Parent $UnityExePath)
        $keytool = Join-Path $unityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe'
        if (Test-Path -LiteralPath $keytool) {
            return $keytool
        }
    }

    $command = Get-Command keytool.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'keytool.exe was not found. Install Unity Android OpenJDK or add JDK bin to PATH.'
}

function Normalize-Fingerprint {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ''
    }

    return ($Value -replace '[^A-Fa-f0-9]', '').ToLowerInvariant()
}

function Get-KeystoreCertificateSha256 {
    param(
        [string]$KeytoolPath,
        [string]$KeystorePath,
        [string]$StorePass,
        [string]$Alias
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $KeytoolPath -list -v -keystore $KeystorePath -storepass $StorePass -alias $Alias 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "keytool failed while reading Android dev signing keystore.`n$($output -join "`n")"
    }

    $line = $output | Select-String -Pattern 'SHA256:' | Select-Object -First 1
    if (-not $line) {
        throw 'keytool output did not contain SHA256 fingerprint.'
    }

    return Normalize-Fingerprint (($line.Line -split 'SHA256:\s*', 2)[1])
}

if ([string]::IsNullOrWhiteSpace($TargetRoot)) {
    $TargetRoot = Get-DefaultTargetRoot
}

$TargetRoot = [System.IO.Path]::GetFullPath($TargetRoot)

if (-not [string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
    Assert-PathExists -Path $PackageRoot -Description 'Android dev signing package root'

    $sourceKeystore = Join-Path $PackageRoot 'LostCyberHamster-dev.keystore'
    $sourceConfig = Join-Path $PackageRoot 'signing.local.json'
    Assert-PathExists -Path $sourceKeystore -Description 'Package keystore'
    Assert-PathExists -Path $sourceConfig -Description 'Package signing config'

    New-Item -ItemType Directory -Force -Path $TargetRoot | Out-Null

    foreach ($destination in @(
        (Join-Path $TargetRoot 'LostCyberHamster-dev.keystore'),
        (Join-Path $TargetRoot 'signing.local.json')
    )) {
        if ((Test-Path -LiteralPath $destination) -and -not $Force.IsPresent) {
            throw "Target signing file already exists: $destination. Rerun with -Force to overwrite."
        }
    }

    Copy-Item -LiteralPath $sourceKeystore -Destination (Join-Path $TargetRoot 'LostCyberHamster-dev.keystore') -Force
    Copy-Item -LiteralPath $sourceConfig -Destination (Join-Path $TargetRoot 'signing.local.json') -Force
}

$configPath = Join-Path $TargetRoot 'signing.local.json'
Assert-PathExists -Path $configPath -Description 'Android dev signing config'

$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($property in @('keystorePath', 'keystorePass', 'keyaliasName', 'keyaliasPass')) {
    if (-not ($config.PSObject.Properties.Name -contains $property) -or [string]::IsNullOrWhiteSpace($config.$property)) {
        throw "Android dev signing config field '$property' is missing: $configPath"
    }
}

$keystorePath = Resolve-ConfiguredPath -Path $config.keystorePath -BaseDirectory $TargetRoot
Assert-PathExists -Path $keystorePath -Description 'Android dev signing keystore'

$keytool = Get-KeytoolPath -UnityExePath $UnityExe
$actualSha256 = Get-KeystoreCertificateSha256 `
    -KeytoolPath $keytool `
    -KeystorePath $keystorePath `
    -StorePass $config.keystorePass `
    -Alias $config.keyaliasName

$configuredSha256 = if ($config.PSObject.Properties.Name -contains 'certificateSha256') {
    Normalize-Fingerprint $config.certificateSha256
}
else {
    ''
}

if (-not [string]::IsNullOrWhiteSpace($configuredSha256) -and $configuredSha256 -ne $actualSha256) {
    throw "Configured certificateSha256 does not match keystore fingerprint. configured=$configuredSha256 actual=$actualSha256"
}

[pscustomobject]@{
    signingConfigPath = $configPath
    keystorePath = $keystorePath
    keyAliasName = $config.keyaliasName
    certificateSha256 = $actualSha256
    certificateMatchesConfig = if ([string]::IsNullOrWhiteSpace($configuredSha256)) { $null } else { $true }
} | ConvertTo-Json -Depth 4
