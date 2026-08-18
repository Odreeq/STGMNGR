[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.0.3',

    [string]$InnoSetupPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$projectPath = Join-Path $repositoryRoot 'StageManager\StageManager.csproj'
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishDirectory = Join-Path $artifactsRoot 'publish\win-x64'
$installerDirectory = Join-Path $artifactsRoot 'installer'
$versionInfoVersion = "$Version.0"

function Remove-BuildDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $allowedPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the artifacts directory: $resolvedPath"
    }

    if (Test-Path -LiteralPath $resolvedPath) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }
}

$dotnet = Get-Command dotnet -ErrorAction Stop
$installedSdks = & $dotnet.Source --list-sdks
if (-not $installedSdks) {
    throw 'The .NET SDK was not found. Install the .NET 8 SDK and try again.'
}

Remove-BuildDirectory -Path $publishDirectory
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null

& $dotnet.Source publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -p:Version=$Version `
    -p:AssemblyVersion=$versionInfoVersion `
    -p:FileVersion=$versionInfoVersion
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$compilerCandidates = @(
    $InnoSetupPath,
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$compilerPath = $compilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (-not $compilerPath) {
    $isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $isccCommand) {
        $compilerPath = $isccCommand.Source
    }
}

if (-not $compilerPath) {
    throw 'Inno Setup 6 was not found. Install it and try again, or specify ISCC.exe with -InnoSetupPath.'
}

$innoScript = Join-Path $PSScriptRoot 'StageManager.iss'
& $compilerPath `
    "/DMyAppVersion=$Version" `
    "/DMyVersionInfoVersion=$versionInfoVersion" `
    "/DSourceDir=$publishDirectory" `
    "/DOutputDir=$installerDirectory" `
    $innoScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $installerDirectory "StageBar-Setup-$Version-x64.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "The installer was not generated: $installerPath"
}

Write-Output "Installer generated: $installerPath"
