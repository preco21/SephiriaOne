param(
    [string]$BinaryPath,

    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BinaryPath)) {
    $outputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\SephiriaOne\bin'))
    foreach ($configuration in @('Release', 'Debug')) {
        $candidate = Join-Path $outputRoot "$configuration\netstandard2.1\SephiriaOne.dll"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $BinaryPath = $candidate
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($BinaryPath)) {
        throw "No built SephiriaOne.dll found under $outputRoot. Build the project first or specify -BinaryPath."
    }
}

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

Write-Output "Deployed $BinaryPath and metadata.json to $destination"
