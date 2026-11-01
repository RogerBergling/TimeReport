param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$project = Get-Content (Join-Path $root "src\TimeReport\TimeReport.csproj")
    $Version = $project.Project.PropertyGroup.Version | Select-Object -First 1
}
$binary = Join-Path $root "dist\windows\TimeReport.exe"
if (-not (Test-Path $binary)) {
    throw "Missing $binary. Publish the Windows executable first."
}

$out = Join-Path $root "dist\packages"
$name = "TimeReport-$Version-windows-x86_64"
$stage = Join-Path $out $name
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item $binary (Join-Path $stage "TimeReport.exe")
Copy-Item (Join-Path $root "LICENSE") (Join-Path $stage "LICENSE")
Copy-Item (Join-Path $root "TERMS.md") (Join-Path $stage "TERMS.md")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $stage "THIRD-PARTY-NOTICES.md")
Copy-Item (Join-Path $root "README.md") (Join-Path $stage "README.md")

$zip = Join-Path $out "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $name.zip" | Set-Content "$zip.sha256"
Remove-Item $stage -Recurse -Force

Write-Host "Created $zip"
Get-ChildItem "$zip*" | Format-Table Name, Length
