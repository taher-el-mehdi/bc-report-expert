# Updates the embedded AL TextMate grammar from Microsoft's official repository.
# Requires Node.js (npx plist2).

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$syntaxDir = Join-Path $repoRoot "src\ReportExpert.Editors\Resources\Syntax"
$plistPath = Join-Path $syntaxDir "alsyntax.tmlanguage"
$jsonPath = Join-Path $syntaxDir "al.tmLanguage.json"
$sourceUrl = "https://raw.githubusercontent.com/microsoft/AL/master/grammar/alsyntax.tmlanguage"

New-Item -ItemType Directory -Force -Path $syntaxDir | Out-Null

Write-Host "Downloading $sourceUrl ..."
Invoke-WebRequest -Uri $sourceUrl -OutFile $plistPath

Write-Host "Converting plist to JSON ..."
npx --yes plist2 $plistPath $jsonPath

if (-not (Test-Path $jsonPath)) {
    throw "Grammar conversion failed; $jsonPath was not created."
}

Write-Host "AL grammar updated: $jsonPath"
