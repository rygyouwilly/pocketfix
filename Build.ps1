param(
    [switch]$StageOnly,
    [string]$harmony = 'D:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll'
)

$ErrorActionPreference = 'Stop'

$sourceRoot = $PSScriptRoot
$outputRoot = Join-Path $sourceRoot 'Assemblies'
$stagingRoot = Join-Path $sourceRoot 'BuildOutput'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null

$buildOutput = if ($StageOnly) {
    Join-Path $stagingRoot 'PocketMapShuttleLaunchFix.dll'
} else {
    Join-Path $outputRoot 'PocketMapShuttleLaunchFix.dll'
}

# 1. Determine the reference root directory dynamically
$localRefDir = Join-Path $PSScriptRoot 'References'
$compilerArgs = @(
    '/nologo',
    '/target:library',
    '/optimize+',
    '/debug:pdbonly',
    '/nowarn:0162,0429,1684',
    ('/out:' + $buildOutput)
)

if (Test-Path (Join-Path $localRefDir 'Assembly-CSharp.dll')) {
    Write-Host "Compiling using detached localized References folder."
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'Assembly-CSharp.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'Waylith.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'UnityStubs.dll'))
} else {
    Write-Host "References folder not found. Compiling via standard hardcoded disk locations."
    $fallbackRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\Unity.Mathematics.dll'))
}

# 2. Add the validated Harmony library path
$compilerArgs += ('/reference:' + $harmony)

# 3. Add explicit code assets
$compilerArgs += @(
    (Join-Path $sourceRoot 'ModBootstrap.cs'),
    (Join-Path $sourceRoot 'PocketMapOriginResolver.cs'),
    (Join-Path $sourceRoot 'LaunchPatches.cs'),
    (Join-Path $sourceRoot 'CargoDialogPatches.cs'),
    (Join-Path $sourceRoot 'PocketLandingPatches.cs'),
    (Join-Path $sourceRoot 'Properties\AssemblyInfo.cs')
)

# Run compiler and pipe both stdout and stderr straight into the action console log
Write-Host "Invoking C# Compiler..."
& $compiler $compilerArgs 2>&1 | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -ne 0) {
    throw "C# compiler failed with exit code $LASTEXITCODE. Review the detailed log above for missing types or syntax issues."
}
