param([switch]$StageOnly)

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
    # ENVIRONMENT A: Running on GitHub Actions runner or local repo using option A layout
    Write-Host "Compiling using detached localized References folder."
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'Assembly-CSharp.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'Waylith.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'UnityStubs.dll'))
} else {
    # ENVIRONMENT B: Running on your absolute local PC path fallback configuration
    Write-Host "References folder not found. Compiling via standard hardcoded disk locations."
    $fallbackRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\Unity.Mathematics.dll'))
}

# 2. Add the dynamic Harmony path provided via script variable tracking
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

& $compiler $compilerArgs

if ($LASTEXITCODE -ne 0) {
    throw "C# compiler exited with code $LASTEXITCODE."
}
