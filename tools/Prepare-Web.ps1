param([string]$Source = 'Builds/Web', [string]$Destination = 'web')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath = [IO.Path]::GetFullPath((Join-Path $projectRoot $Source))
$destinationPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $Destination))
if ($sourcePath -ne (Join-Path $projectRoot 'Builds\Web') -or $destinationPath -ne (Join-Path $projectRoot 'web')) {
    throw 'This helper only publishes Builds/Web into the project web folder.'
}
if (!(Test-Path -LiteralPath (Join-Path $sourcePath 'index.html'))) { throw 'Build Web in Unity first.' }
# Delete only the explicitly checked publication folder; source assets/builds stay intact.
if (Test-Path -LiteralPath $destinationPath) {
    if ((Get-Item -LiteralPath $destinationPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'web cannot be a link.' }
    Remove-Item -LiteralPath $destinationPath -Recurse -Force
}
New-Item -ItemType Directory -Path $destinationPath | Out-Null
Get-ChildItem -LiteralPath $sourcePath -Force | Copy-Item -Destination $destinationPath -Recurse -Force
[IO.File]::WriteAllText((Join-Path $destinationPath '.nojekyll'), '')
Write-Output 'Prepared web/. Review and commit it with the GitHub Pages workflow.'
