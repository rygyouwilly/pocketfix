param([switch]$StageOnly)

$ErrorActionPreference = 'Stop'

$rimWorldRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$sourceRoot = $PSScriptRoot
$outputRoot = Join-Path $sourceRoot '..\Assemblies'
$stagingRoot = Join-Path $sourceRoot 'BuildOutput'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$harmony = 'D:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll'

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null

$buildOutput = if ($StageOnly) {
    Join-Path $stagingRoot 'PocketMapShuttleLaunchFix.dll'
} else {
    Join-Path $outputRoot 'PocketMapShuttleLaunchFix.dll'
}

$compilerArgs = @(
    '/nologo',
    '/target:library',
    '/optimize+',
    '/debug:pdbonly',
    '/nowarn:0162,0429,1684',
    ('/out:' + $buildOutput),
    ('/reference:' + (Join-Path $rimWorldRoot 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll')),
    ('/reference:' + $harmony),
    ('/reference:' + (Join-Path $rimWorldRoot 'RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll')),
    ('/reference:' + (Join-Path $rimWorldRoot 'RimWorldWin64_Data\Managed\Unity.Mathematics.dll')),
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
