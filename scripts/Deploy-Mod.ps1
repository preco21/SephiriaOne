param(
    [Parameter(Mandatory = $true)]
    [string]$BinaryPath,

    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria'
)

$ErrorActionPreference = 'Stop'

$metadataPath = Join-Path (Split-Path -Parent $BinaryPath) 'metadata.json'
foreach ($sourcePath in @($BinaryPath, $metadataPath)) {
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Deployment source is missing: $sourcePath. Build the project first."
    }
}

$modName = [IO.Path]::GetFileNameWithoutExtension($BinaryPath)
$destination = Join-Path (Join-Path $GameDir 'AddOns') $modName

New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $BinaryPath -Destination $destination -Force
Copy-Item -LiteralPath $metadataPath -Destination $destination -Force

Write-Output "Deployed $modName.dll and metadata.json to $destination"
