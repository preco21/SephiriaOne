param(
    [string]$BinaryPath = (Join-Path $PSScriptRoot '..\SephiriaOne\bin\Release\netstandard2.1\SephiriaOne.dll'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts')
)

$ErrorActionPreference = 'Stop'
$binary = [IO.Path]::GetFullPath($BinaryPath)
$metadataPath = Join-Path (Split-Path -Parent $binary) 'metadata.json'
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) {
    throw 'Build Release with -p:DeployMod=false before packaging.'
}
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$assembly = [Reflection.AssemblyName]::GetAssemblyName($binary)
if ($metadata.modName -ne 'SephiriaOne' -or $metadata.entryClass -ne 'SephiriaOne.Entry' -or
    $metadata.dllFile -ne 'SephiriaOne.dll' -or $assembly.Name -ne 'SephiriaOne' -or
    $metadata.modVersion -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or
    $assembly.Version.ToString(3) -ne $metadata.modVersion -or $assembly.Version.Revision -gt 0) {
    throw 'Release DLL and metadata identities/versions do not match.'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$archivePath = Join-Path $output 'SephiriaOne.zip'
$temporary = Join-Path $output ('SephiriaOne.' + [Guid]::NewGuid().ToString('N') + '.tmp')
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    $archive = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $binary, 'SephiriaOne.dll') | Out-Null
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $metadataPath, 'metadata.json') | Out-Null
    } finally { $archive.Dispose() }
    if (Test-Path -LiteralPath $archivePath) { [IO.File]::Replace($temporary, $archivePath, [NullString]::Value) }
    else { [IO.File]::Move($temporary, $archivePath) }
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
$digest = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Packaged v$($metadata.modVersion): $archivePath"
Write-Output "SHA256: $digest"
