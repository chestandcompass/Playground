param([string]$GameDirectory = (Join-Path $PSScriptRoot 'runtime'))
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $release = Get-Content release.json -Raw | ConvertFrom-Json
    if ($release.version -notmatch '^\d+\.\d+\.\d+$' -or $release.build -lt 1 -or $release.tag -notmatch '^v[0-9A-Za-z.-]+$') { throw 'Invalid release.json.' }
    $info = Get-Content SR2MP\BuildInfoConstants.cs -Raw
    $info = $info -replace 'const string MelonVersion = "[^"]+"', ('const string MelonVersion = "'+$release.version+'"')
    $info = $info -replace 'const string Version = "[^"]+"', ('const string Version = "'+$release.version+'"')
    $info = $info -replace 'const int UpdateBuild = \d+', ('const int UpdateBuild = '+$release.build)
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'SR2MP\BuildInfoConstants.cs'), $info)
    $sdk = Join-Path $PSScriptRoot '.local\dotnet\dotnet.exe'
    if (!(Test-Path $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
    & $sdk build SR2MP -c Release --nologo -p:RunAnalyzers=false
    if ($LASTEXITCODE -ne 0) { throw 'Playground build failed.' }
    $cache = Join-Path $PSScriptRoot '.local'
    New-Item -ItemType Directory -Force $cache,dist | Out-Null
    $loader = Join-Path $cache 'MelonLoader.zip'
    if (!(Test-Path $loader)) {
        Invoke-WebRequest 'https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip' -OutFile $loader
    }
    $runtime = Join-Path $cache 'dotnet-runtime-6.0.36-win-x64.exe'
    $releaseMetadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/6.0/releases.json'
    $runtimeInfo = $releaseMetadata.releases | Where-Object { $_.'release-version' -eq '6.0.36' } | Select-Object -First 1 -ExpandProperty runtime
    $runtimeFile = $runtimeInfo.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.exe' } | Select-Object -First 1
    if (!(Test-Path $runtime)) { Invoke-WebRequest $runtimeFile.url -OutFile $runtime }
    if ((Get-FileHash $runtime -Algorithm SHA512).Hash.ToLowerInvariant() -ne $runtimeFile.hash) { throw 'Microsoft runtime hash mismatch.' }
    $stage = Join-Path $cache 'installer-payload'
    New-Item -ItemType Directory -Force "$stage\Mods","$stage\Dependencies" | Out-Null
    Copy-Item SR2MP\bin\Release\net6.0\Playground.dll "$stage\Mods\Playground.dll" -Force
    Copy-Item $loader "$stage\Dependencies\MelonLoader-0.7.3-x64.zip" -Force
    Copy-Item $runtime "$stage\Dependencies\dotnet-runtime-6.0.36-win-x64.exe" -Force
    Copy-Item LICENSE "$stage\LICENSE" -Force
    $feed = [ordered]@{
        schema=1; version=$release.version; build=$release.build; gameVersion=$release.gameVersion; channel=$release.channel;
        dllUrl=('https://github.com/robertbrania-code/Playground/releases/download/'+$release.tag+'/Playground.dll');
        sha256=(Get-FileHash "$stage\Mods\Playground.dll" -Algorithm SHA256).Hash.ToLowerInvariant();
        gameAssemblySha256=(Get-FileHash (Join-Path $GameDirectory 'GameAssembly.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $feed | ConvertTo-Json | Set-Content "$stage\update.json" -Encoding UTF8
    $payloadZip = Join-Path $cache 'installer-payload.zip'
    Compress-Archive -Path "$stage\*" -DestinationPath $payloadZip -Force
    $hash = (Get-FileHash $payloadZip -Algorithm SHA256).Hash.ToLowerInvariant()
    $generated = Join-Path $cache 'PayloadInfo.cs'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($loader)
    try {
        $stream = $archive.GetEntry('version.dll').Open()
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { $proxyHash = ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
        finally { $algorithm.Dispose(); $stream.Dispose() }
    }
    finally { $archive.Dispose() }
    [IO.File]::WriteAllText($generated, 'namespace PlaygroundSetup { internal static class PayloadInfo { internal const string Sha256 = "' + $hash + '"; internal const string ProxySha256 = "' + $proxyHash + '"; } }')
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output `
        /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll `
        /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
        "/resource:$payloadZip,Payload" /out:dist\PlaygroundSetup.exe installer\Program.cs installer\Maintenance.cs installer\GameDiscovery.cs $generated
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $feed.setupUrl='https://github.com/robertbrania-code/Playground/releases/download/'+$release.tag+'/PlaygroundSetup.exe'
    $feed.setupSha256=(Get-FileHash dist\PlaygroundSetup.exe -Algorithm SHA256).Hash.ToLowerInvariant()
    $feed | ConvertTo-Json | Set-Content update.json -Encoding UTF8
    Copy-Item SR2MP\bin\Release\net6.0\Playground.dll dist\Playground.dll -Force
    Get-Item dist\PlaygroundSetup.exe,dist\Playground.dll | Select-Object Name,Length
}
finally { Pop-Location }
