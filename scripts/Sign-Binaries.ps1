<#
.SYNOPSIS
    Signe (Authenticode) les fichiers passés en paramètre avec le certificat AGYTEK, si les secrets
    de signature sont présents dans l'environnement.

.DESCRIPTION
    Utilisé par les workflows CI (build.yml, publish-release.yml). Ne fait rien (code de sortie 0)
    si CODE_SIGNING_PFX_BASE64 / CODE_SIGNING_PASSWORD ne sont pas définies : tant que ces secrets
    GitHub n'ont pas été configurés par un administrateur du dépôt, la CI continue de produire des
    binaires non signés plutôt que d'échouer. Voir docs/CODE_SIGNING.md pour la mise en place.
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]] $Path
)

$ErrorActionPreference = "Stop"

$pfxBase64 = $env:CODE_SIGNING_PFX_BASE64
$pfxPassword = $env:CODE_SIGNING_PASSWORD

if ([string]::IsNullOrEmpty($pfxBase64) -or [string]::IsNullOrEmpty($pfxPassword)) {
    Write-Host "Secrets de signature absents (CODE_SIGNING_PFX_BASE64 / CODE_SIGNING_PASSWORD) : binaires non signés."
    exit 0
}

$files = @(Get-ChildItem -Path $Path -ErrorAction Stop)
if ($files.Count -eq 0) {
    Write-Warning "Aucun fichier ne correspond à $($Path -join ', ') : rien à signer."
    exit 0
}

$signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*\x64\*" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $signtool) {
    throw "signtool.exe introuvable sous Windows Kits. Le SDK Windows est-il installé sur ce runner ?"
}

$pfxPath = Join-Path $env:RUNNER_TEMP "agytek-codesign.pfx"
[IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($pfxBase64))

try {
    foreach ($file in $files) {
        Write-Host "Signature de $($file.FullName)..."
        & $signtool sign /f $pfxPath /p $pfxPassword /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 $file.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "signtool a échoué (code $LASTEXITCODE) pour $($file.FullName)"
        }
    }
}
finally {
    Remove-Item $pfxPath -Force -ErrorAction SilentlyContinue
}
