param(
    [switch]$StageOnly,
    [string]$harmony = ''
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

# 1. Base compiler configuration arguments
$compilerArgs = @(
    '/nologo',
    '/target:library',
    '/optimize+',
    '/debug:pdbonly',
    '/nowarn:0162,0429,1684',
    ('/out:' + $buildOutput)
)

# 2. Check for modern Waylith dependencies in a local 'References' folder first
$localRefDir = Join-Path $PSScriptRoot 'References'
if (Test-Path (Join-Path $localRefDir 'Assembly-CSharp.dll')) {
    Write-Host "Compiling using localized cloud/local References folder."
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'Assembly-CSharp.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'Waylith.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $localRefDir 'UnityStubs.dll'))
} else {
    # Fallback absolute path loop if running on your local machine workspace root
    Write-Host "References folder empty or missing. Checking legacy game folders..."
    $fallbackRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll'))
    $compilerArgs += ('/reference:' + (Join-Path $fallbackRoot 'RimWorldWin64_Data\Managed\Unity.Mathematics.dll'))
}

# 3. Securely bind the Harmony dependency if passed from the workflow
if (-not [string]::IsNullOrWhiteSpace($harmony)) {
    Write-Host "Binding verified Harmony reference: $harmony"
    $compilerArgs += ('/reference:' + $harmony)
} else {
    # Local fallback for your hardcoded PC folder if executed directly
    $localHarmony = 'D:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll'
    Write-Host "Using default local Harmony assembly target path: $localHarmony"
    $compilerArgs += ('/reference:' + $localHarmony)
}

# 4. DYNAMIC SOURCE DISCOVERY: Finds every single C# file in the directory automatically
Write-Host "Scanning workspace for C# source files..."
$sourceFiles = Get-ChildItem -Path $sourceRoot -Filter "*.cs" -Recurse
foreach ($file in $sourceFiles) {
    Write-Host " -> Staging file for build: $($file.FullName)"
    $compilerArgs += $file.FullName
}

if ($sourceFiles.Count -eq 0) {
    throw "CRITICAL ERROR: No C# source files (*.cs) were discovered in the directory workspace!"
}

# 5. Run the compiler tool
Write-Host "Invoking C# compiler tool..."
& $compiler $compilerArgs

if ($LASTEXITCODE -ne 0) {
    throw "C# compiler exited with structural code $LASTEXITCODE."
}

Write-Host "Build finished successfully! Assembly compiled at: $buildOutput"
