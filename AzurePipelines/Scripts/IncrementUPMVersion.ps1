<#
.SYNOPSIS
    This script is used by the pipeline to append the prerelease id to the UPM package, and optionally increment the minor version.
.DESCRIPTION
    This script is used by the pipeline to append the prerelease id to the UPM package, and optionally increment the minor version.
.PARAMETER PrereleaseID
    The prerelease ID to set.
.PARAMETER IncrementPatch
    Whether to increment the minor version.
#>
param(
    [string]$PrereleaseID,
    [switch]$IncrementPatch
)

function Get-ScriptDirectory {
    Split-Path -parent $PSCommandPath
}

$PathToPackage=Get-ScriptDirectory
$PathToPackage=Join-Path $PathToPackage "..\..\Source\MSBuildTools.Unity\Packages\com.microsoft.msbuildforunity\package.json"
$rawFileData = Get-Content -Raw -Path $PathToPackage
$jsondata = $rawFileData | ConvertFrom-Json

Write-Host "Original version in the file: $($jsondata.version)"
$rawVersionParts = $jsondata.version -split "-"
$version = [version]$rawVersionParts[0]
Write-Host "Prerelease stripped version: $($version)"

if ($IncrementPatch) {
    $version = New-Object -TypeName System.Version -ArgumentList $version.Major, $version.Minor, ($version.Build + 1)
}

Write-Host "Post optional increment version: $($version)"

$version = "{0}-{1}" -f $version, $PrereleaseID
Write-Host "After appending pre-release id: $($version)"

$rawFileData -replace $jsondata.version, $version | Out-File $PathToPackage