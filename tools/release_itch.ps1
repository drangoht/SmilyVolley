<#
.SYNOPSIS
    Publie la version web de Smily Volley sur itch.io.

.DESCRIPTION
    Enchaine : numero de version pose dans le projet -> build Unity (scene + WebGL) -> verification
    du tampon produit par le build -> dossier de distribution propre -> `butler push`.

    Repris de tools/release_unity.ps1 de Chimera Protocol, reduit a la seule cible web. Les
    garde-fous conserves sont ceux qui avaient une raison d'etre : on verifie que ce qu'on pousse
    est bien ce qu'on vient de construire, parce qu'une release a deja expedie le binaire de la
    version precedente sans qu'aucune erreur ne soit levee.

.PARAMETER Version
    Numero affiche sur itch (ex. 1.0.0). Obligatoire : rien ne le declare ailleurs dans le depot,
    le poser ICI est la decision de publier.

.PARAMETER SkipBuild
    Reutilise le dossier Build/Web deja present. A n'employer que si l'on vient de le construire
    soi-meme : le script verifie de toute facon que son tampon porte la version demandee.

.PARAMETER DryRun
    Va jusqu'au dossier de distribution et s'arrete AVANT butler et avant tout commit. C'est le seul
    moyen d'eprouver la chaine sans publier : un script de release qu'on ne peut essayer qu'en
    publiant ne se teste jamais qu'en production.

.EXAMPLE
    & "tools/release_itch.ps1" -Version 1.0.0 -DryRun
    & "tools/release_itch.ps1" -Version 1.0.0
#>

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Itch = "drangoht/smily-volley",
    # ATTENTION : le nom du canal decide, cote itch.io, si le fichier est JOUABLE DANS LE NAVIGATEUR.
    # `html5` (ou `html`, ou `web`) est reconnu comme tel ; tout autre nom produit une archive a
    # telecharger, qui s'installe parfaitement et ne se joue pas. Rien ne le signale.
    [string]$Channel = "html5",
    [switch]$SkipBuild,
    [switch]$DryRun
)

# NB : PAS "Stop". Unity, git et butler ecrivent leur progression sur stderr, ce que PowerShell 5.1
# prend pour une erreur. Seul $LASTEXITCODE fait foi apres un executable natif.
$ErrorActionPreference = "Continue"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Unity       = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe"
$BuildDir    = Join-Path $ProjectRoot "Build\Web"
$Staging     = Join-Path $ProjectRoot "Build\staging-web"
$Settings    = Join-Path $ProjectRoot "ProjectSettings\ProjectSettings.asset"

# index.html : la page elle-meme. Build\ : le wasm, les donnees et le chargeur. build_stamp.json :
# la carte d'identite de ce qui vient d'etre construit, seul controle de fraicheur honnete.
$Required = @("index.html", "Build", "build_stamp.json")

function Fail($msg) { Write-Host "ERREUR : $msg" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $Unity)) { Fail "Unity introuvable : $Unity" }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { Fail "Version attendue au format x.y.z (recu : $Version)" }

# --- Butler ------------------------------------------------------------------------
# Fourni par l'app itch.io (dossier broth), qui le tient a jour toute seule.
$brothGlob = Join-Path $env:APPDATA "itch\broth\butler\versions\*\butler.exe"
$butler = Get-ChildItem -Path $brothGlob -ErrorAction SilentlyContinue |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $butler) {
    Fail "butler.exe introuvable. Lance l'app itch.io une fois, ou installe butler depuis https://itchio.itch.io/butler"
}
$Butler = $butler.FullName

Write-Host "Butler  : $Butler" -ForegroundColor Cyan
Write-Host "Version : $Version  ->  $Itch`:$Channel" -ForegroundColor Cyan

# --- 1. Version dans les reglages du projet ----------------------------------------
# C'est elle que lit Application.version, donc le tampon affiche en bas a droite du jeu.
$content = Get-Content $Settings -Raw
$content = $content -replace '(?m)^(\s*bundleVersion:\s*).*$', "`${1}$Version"
Set-Content -Path $Settings -Value $content -Encoding utf8 -NoNewline
Write-Host "bundleVersion pose a $Version." -ForegroundColor DarkGray

# --- 2. Build ----------------------------------------------------------------------
if (-not $SkipBuild) {
    if (Get-Process Unity -ErrorAction SilentlyContinue) {
        Fail "L'editeur Unity est ouvert : le build en ligne de commande echouerait. Ferme-le et relance."
    }

    $log = Join-Path $env:TEMP "smily-release-web.log"
    Write-Host "Build web en cours (log : $log)..." -ForegroundColor Yellow

    # Start-Process et non l'operateur d'appel `&` : lance par `&`, Unity rend la main
    # IMMEDIATEMENT sans rien faire, $LASTEXITCODE vide, et le script poursuit comme si tout allait
    # bien. Un lancement qui echoue en silence est pire qu'un lancement qui echoue.
    $proc = Start-Process -FilePath $Unity -PassThru -Wait -NoNewWindow -ArgumentList @(
        "-batchmode", "-quit",
        "-projectPath", $ProjectRoot,
        "-logFile", $log,
        "-executeMethod", "SmilyVolley.EditorTools.BuildTools.RebuildWeb"
    )

    if ($proc.ExitCode -ne 0) { Fail "Build Unity echoue (code $($proc.ExitCode)) - voir $log" }
    if (-not (Test-Path $log)) { Fail "Build Unity : aucun journal ecrit ($log) - Unity n'a pas demarre." }

    # Un code retour nul ne distingue pas « construit » de « rien a faire » : on exige la reussite
    # explicitement annoncee par BuildTools.
    if (-not (Select-String -Path $log -Pattern 'Build web reussi|Build web réussi' -Quiet)) {
        Fail "Build web : aucune reussite confirmee dans $log"
    }
} else {
    Write-Host "SkipBuild : dossier existant reutilise." -ForegroundColor DarkGray
    if (-not (Test-Path $BuildDir)) { Fail "SkipBuild demande mais aucun build : $BuildDir" }
}

# --- 3. Verification du build ------------------------------------------------------
foreach ($required in $Required) {
    if (-not (Test-Path (Join-Path $BuildDir $required))) { Fail "Element manquant dans le build : $required" }
}

# Le tampon produit PAR le build : dernier point ou l'on peut constater qu'on s'apprete a publier
# autre chose que ce qu'on croit. La date du dossier ne prouve rien, le build etant incremental.
$stamp = Get-Content (Join-Path $BuildDir "build_stamp.json") -Raw | ConvertFrom-Json
if ($stamp.version -ne $Version) {
    Fail "Le build porte la version '$($stamp.version)' alors qu'on publie '$Version' - build perime."
}
Write-Host "Build verifie : v$($stamp.version)-$($stamp.sha) (construit le $($stamp.date))." -ForegroundColor DarkGray

# Le suffixe « + » dit que l'arbre de travail portait des modifications : le build ne correspond
# alors A AUCUN COMMIT, et le tampon affiche en jeu ne permettra pas de rejouer un rapport de bug.
if ($stamp.sha -like "*+") {
    Write-Host "AVERTISSEMENT : build issu d'un arbre modifie ($($stamp.sha)) - il ne correspond a aucun commit." -ForegroundColor Yellow
} elseif ($stamp.sha -eq "dev") {
    Write-Host "AVERTISSEMENT : le build n'a pas pu lire git - le tampon dira 'dev' aux joueurs." -ForegroundColor Yellow
}

# --- 4. Dossier de distribution propre ---------------------------------------------
# Butler diffe fichier par fichier : on pousse un DOSSIER, sans les artefacts que le build depose a
# cote (symboles Burst nommes DoNotShip par Unity elle-meme).
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
New-Item -ItemType Directory -Path $Staging -Force | Out-Null

Copy-Item (Join-Path $BuildDir "*") -Destination $Staging -Recurse -Force -Exclude "*BurstDebugInformation*"
Get-ChildItem $Staging -Directory -Filter "*BurstDebugInformation*" |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$poids = [math]::Round((Get-ChildItem $Staging -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Staging pret : $Staging ($poids Mo)" -ForegroundColor Cyan

# --- 5. Push Butler ----------------------------------------------------------------
if ($DryRun) {
    Write-Host "`nA BLANC : tout est pret, rien n'a ete publie." -ForegroundColor Green
    Write-Host "  build   : $BuildDir" -ForegroundColor DarkGray
    Write-Host "  staging : $Staging" -ForegroundColor DarkGray
    Write-Host "  tampon  : v$($stamp.version)-$($stamp.sha)" -ForegroundColor DarkGray
    Write-Host "Relance sans -DryRun pour pousser sur $Itch`:$Channel." -ForegroundColor Green
    exit 0
}

Write-Host "Push vers itch.io..." -ForegroundColor Yellow
& $Butler push $Staging "$Itch`:$Channel" --userversion $Version
if ($LASTEXITCODE -ne 0) {
    Fail "butler push echoue (code $LASTEXITCODE). Si 'not authorized', lance une fois : `"$Butler`" login"
}

# --- 6. Commit du numero de version ------------------------------------------------
Push-Location $ProjectRoot
git add "ProjectSettings/ProjectSettings.asset"
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git commit -m "chore(release): $Version (canal web)"
    if ($LASTEXITCODE -eq 0) {
        git push
        if ($LASTEXITCODE -ne 0) {
            Write-Host "AVERTISSEMENT : git push echoue - pousse le commit de version a la main." -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "Rien a committer (numero de version inchange)." -ForegroundColor DarkGray
}
Pop-Location

# --- 7. Etat ------------------------------------------------------------------------
& $Butler status $Itch
Write-Host "`nPublication OK - version $Version poussee sur $Itch`:$Channel" -ForegroundColor Green
Write-Host "La page sert le nouveau build des qu'itch a fini de le traiter." -ForegroundColor Green
Write-Host "Prerequis cote itch.io, a faire UNE fois : « Kind of project » = HTML," -ForegroundColor Yellow
Write-Host "et le fichier coche « This file will be played in the browser »." -ForegroundColor Yellow
