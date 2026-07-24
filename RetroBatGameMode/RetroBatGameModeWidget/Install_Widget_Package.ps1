# ==============================================================================
# RetroBatGameModeWidget -- Package Installer (PowerShell, EN/FR bilingual)
# EN: Same logic as VS-generated Add-AppDevPackage.ps1:
#     1. Find signed MSIX in the script's own folder (no recursion).
#        Prefer WAP-built (.Package_ in name = signed by VS).
#     2. If cert not yet trusted: install to CurrentUser\TrustedPeople (no Admin)
#     3. Add-AppxPackage -ForceApplicationShutdown
# FR: Meme logique que le Add-AppDevPackage.ps1 genere par VS:
#     1. Trouver le MSIX signe dans le dossier du script (pas de recursion).
#        Preferer le WAP (.Package_ dans le nom = signe par VS).
#     2. Si cert pas encore approuve : installer dans CurrentUser\TrustedPeople (sans Admin)
#     3. Add-AppxPackage -ForceApplicationShutdown
# ==============================================================================

param(
    [string]$SearchPath = $PSScriptRoot
)

# ------------------------------------------------------------------
# Detect UI language.
# ------------------------------------------------------------------
$uiCulture = [System.Globalization.CultureInfo]::CurrentUICulture.Name
$isFrench = $uiCulture -like 'fr*'

function Loc($en, $fr) {
    if ($isFrench) { return $fr } else { return $en }
}

# ------------------------------------------------------------------
# EN/FR: Find the MSIX -- prefer WAP-built (.Package_ in name = signed by VS).
#        Search ONLY the script's own folder (no recursion).
# ------------------------------------------------------------------
$msix = Get-ChildItem -Path $SearchPath -Filter "*.Package_*.msix" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notlike "*Debug*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

if (-not $msix) {
    $msix = Get-ChildItem -Path $SearchPath -Filter "*.msix" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notlike "*Debug*" } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
}

if (-not $msix) {
    # Last chance: subfolders (developer convenience, not for production).
    $msix = Get-ChildItem -Path $SearchPath -Filter "*.Package_*.msix" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notlike "*Debug*" } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
}

if (-not $msix) {
    Write-Host (Loc "[ERROR] No .msix file found in: $SearchPath" `
                     "[ERROR] Aucun fichier .msix trouve dans : $SearchPath") -ForegroundColor Red
    Write-Host (Loc "        The .msix must sit next to Install_Widget_Package.bat." `
                     "        Le .msix doit etre a cote de Install_Widget_Package.bat.") -ForegroundColor Yellow
    Write-Host (Loc "        Compile via the RetroBatGameModeWidget.Package wapproj in VS" `
                     "        Compilez via le projet WAP RetroBatGameModeWidget.Package dans VS") -ForegroundColor Yellow
    Write-Host (Loc "        (right-click -> Publish -> Create App Packages)." `
                     "        (clic droit -> Publier -> Creer des packages).") -ForegroundColor Yellow
    exit 1
}

Write-Host (Loc "[INFO] Package found: $($msix.FullName)" `
                 "[INFO] Package trouve : $($msix.FullName)") -ForegroundColor Cyan

# ------------------------------------------------------------------
# EN/FR: Check if the MSIX signature is already trusted.
# ------------------------------------------------------------------
$signature = Get-AuthenticodeSignature $msix.FullName
$signerCert = $signature.SignerCertificate

if (-not $signerCert) {
    Write-Host (Loc "[ERROR] The MSIX is not digitally signed." `
                     "[ERROR] Le MSIX n'est pas signe numeriquement.") -ForegroundColor Red
    Write-Host (Loc "        Compile via the RetroBatGameModeWidget.Package wapproj in VS." `
                     "        Compilez via le projet WAP RetroBatGameModeWidget.Package dans VS.") -ForegroundColor Yellow
    exit 1
}

$needInstallCert = ($signature.Status -ne "Valid")

if ($needInstallCert) {
    Write-Host (Loc "[INFO] Certificate not yet trusted. Installing to CurrentUser\TrustedPeople..." `
                     "[INFO] Certificat non encore approuve. Installation dans CurrentUser\TrustedPeople...") -ForegroundColor Cyan

    $cerPath = Join-Path $PSScriptRoot "RetroBatGameModeWidget_Signing.cer"
    try {
        [IO.File]::WriteAllBytes($cerPath, $signerCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))

        Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
        Write-Host (Loc "[SUCCESS] Certificate installed to CurrentUser\TrustedPeople (no Admin)." `
                         "[SUCCESS] Certificat installe dans CurrentUser\TrustedPeople (sans Admin).") -ForegroundColor Green

        Remove-Item $cerPath -ErrorAction SilentlyContinue
    } catch {
        Write-Host (Loc "[WARNING] Could not install certificate: $($_.Exception.Message)" `
                         "[WARNING] Impossible d'installer le certificat : $($_.Exception.Message)") -ForegroundColor Yellow
    }
} else {
    Write-Host (Loc "[INFO] Certificate already trusted. No installation needed." `
                     "[INFO] Certificat deja approuve. Aucune installation necessaire.") -ForegroundColor Green
}

# ------------------------------------------------------------------
# EN/FR: Install the MSIX package for the current user (no Admin required).
# ------------------------------------------------------------------
Write-Host ""
Write-Host (Loc "[EXEC] Installing the MSIX package..." `
                 "[EXEC] Installation du package MSIX...") -ForegroundColor Cyan
try {
    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown -ErrorAction Stop
    Write-Host ""
    Write-Host (Loc "[SUCCESS] RetroBat Game Mode widget installed successfully!" `
                     "[SUCCESS] Widget RetroBat Game Mode installe avec succes !") -ForegroundColor Green
    Write-Host (Loc "         Open Win+G and add the widget from the gallery." `
                     "          Ouvrez Win+G et ajoutez le widget depuis la galerie.") -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host (Loc "[ERROR] Installation failed:" `
                     "[ERROR] L'installation a echoue :") -ForegroundColor Red
    Write-Host "        $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
