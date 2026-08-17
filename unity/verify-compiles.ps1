# Compiles the avatar package's editor script against a real VRChat SDK, without opening Unity.
#
# The desktop test suite can only read this file as text - it cannot compile it, because the types it
# uses ship with the SDK and the SDK is not a NuGet package. So a syntax or signature mistake in the
# generator survives a completely green `dotnet test` and is found by a creator instead.
#
# This closes that gap in a few seconds. It borrows the assemblies from any VCC project that has the
# Avatars SDK resolved; it opens nothing, writes nothing into that project, and leaves only a dll in
# the temp folder.
#
# Two things about the references are worth knowing before changing them:
#   - The SDK types (VRCExpressionParameters, VRCExpressionsMenu) live in the package's precompiled
#     plugins under Packages/com.vrchat.avatars/Runtime/VRCSDK/Plugins, NOT in the similarly named
#     VRC.SDK3A.dll that Unity emits into Library/ScriptAssemblies.
#   - -nostdlib+ is required, and then the base class library has to be supplied by hand from Unity's
#     own NetStandard folder. Leaving it out makes every type in System.* appear to be missing and
#     produces a screen of errors that have nothing to do with the actual code.
#
# Usage:  pwsh -File unity/verify-compiles.ps1 [-ProjectPath <a VCC project>] [-UnityVersion 2022.3.22f1]

[CmdletBinding()]
param(
    [string] $ProjectPath,
    [string] $UnityVersion = '2022.3.22f1',
    [string] $TemplateRoot = 'D:\VRC\VCC-Templates'
)

$ErrorActionPreference = 'Stop'

$unityData = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Data"

if (-not (Test-Path $unityData)) {
    Write-Error "Unity $UnityVersion is not installed at $unityData"
}

if (-not $ProjectPath) {
    $ProjectPath = Get-ChildItem $TemplateRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName 'Packages\com.vrchat.avatars') } |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $ProjectPath -or -not (Test-Path $ProjectPath)) {
    Write-Error "No VCC project with the Avatars SDK found. Pass -ProjectPath."
}

Write-Host "SDK from: $ProjectPath"

$source = Join-Path $PSScriptRoot 'com.magicchatbox.avatar\Editor\MagicChatboxAvatarSetup.cs'

if (-not (Test-Path $source)) {
    Write-Error "Editor script not found at $source"
}

$out = Join-Path ([IO.Path]::GetTempPath()) ("mcb-unity-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $out | Out-Null

try {
    $refs = @()
    $refs += Join-Path $unityData 'NetStandard\ref\2.1.0\netstandard.dll'
    $refs += Get-ChildItem (Join-Path $unityData 'NetStandard\compat') -Recurse -Filter *.dll -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
    $refs += Get-ChildItem (Join-Path $unityData 'Managed\UnityEngine') -Filter *.dll | ForEach-Object { $_.FullName }
    $refs += Join-Path $unityData 'Managed\UnityEditor.dll'
    $refs += Get-ChildItem (Join-Path $ProjectPath 'Packages') -Recurse -Filter 'VRCSDK*.dll' -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
    $refs += Get-ChildItem (Join-Path $ProjectPath 'Library\ScriptAssemblies') -Filter 'VRC.SDK*.dll' -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
    $refs = $refs | Where-Object { Test-Path $_ } | Sort-Object -Unique

    Write-Host "references: $($refs.Count)"

    $rsp = Join-Path $out 'compile.rsp'
    $lines = @(
        '-target:library',
        "-out:$out\MagicChatboxAvatarSetup.dll",
        '-nostdlib+',
        '-langversion:9.0',
        '-define:UNITY_EDITOR;UNITY_2022_3_OR_NEWER',
        '-nowarn:CS1701;CS1702;CS0436'
    )
    $refs | ForEach-Object { $lines += "-r:`"$_`"" }
    $lines += "`"$source`""
    Set-Content -Path $rsp -Value $lines -Encoding UTF8

    $log = & dotnet (Join-Path $unityData 'DotNetSdkRoslyn\csc.dll') "@$rsp" 2>&1
    $failed = $LASTEXITCODE -ne 0

    $log | Select-String -Pattern 'error CS|warning CS' | ForEach-Object { Write-Host $_ }

    if ($failed) {
        Write-Error 'the avatar package does not compile against the SDK'
    }

    Write-Host 'the avatar package compiles against the SDK' -ForegroundColor Green
}
finally {
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
}
