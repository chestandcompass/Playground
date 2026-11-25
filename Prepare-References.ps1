param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$interop = Join-Path $GameDirectory 'MelonLoader\Il2CppAssemblies'
$loader = Join-Path $GameDirectory 'MelonLoader\net6'
if (!(Test-Path $interop) -or !(Test-Path $loader)) {
    throw 'Install MelonLoader 0.7.3 and launch your own Slime Rancher 2 installation once to generate IL2CPP references.'
}
$target = Join-Path $PSScriptRoot 'SR2MP\libraries'
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $interop '*.dll') $target -Force
Copy-Item (Join-Path $loader '*.dll') $target -Force
Write-Host 'Local game/loader references prepared. These files must not be published.'
