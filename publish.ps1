# Publishes Genesha as a standalone exe and (re)creates the desktop shortcut.
# Usage: powershell -ExecutionPolicy Bypass -File .\publish.ps1
$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\Genesha\Genesha.csproj"
$outDir = Join-Path $PSScriptRoot "publish"

Write-Host "Publishing self-contained win-x64 build to $outDir ..."
dotnet publish $project -c Release -r win-x64 --self-contained true -o $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "Genesha.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $outDir "Genesha.exe"
$shortcut.WorkingDirectory = $outDir
$shortcut.Description = "Genesha - Notes and Whiteboards"
$shortcut.Save()

Write-Host "Done."
Write-Host "  Exe:      $outDir\Genesha.exe"
Write-Host "  Shortcut: $shortcutPath"
