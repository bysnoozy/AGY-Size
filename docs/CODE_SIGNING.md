# Signature de code (Authenticode) — certificat auto-signé AGYTEK

Les exécutables (`AgySize.App.exe`, `AgySize.Cli.exe`) et l'installeur (`AGY-Size-Setup.msi`) sont
signés avec un certificat **auto-signé**, généré pour un usage **interne AGYTEK uniquement** (pas de
certificat d'une autorité de certification publique, qui coûte et n'a de sens que pour une diffusion
publique). Un certificat auto-signé retire l'avertissement "éditeur inconnu" de Windows/SmartScreen
**uniquement sur les postes où il a été explicitement installé comme "Éditeur de confiance"** (voir
plus bas) ; sur un poste où il n'a pas été installé, l'avertissement reste identique à aujourd'hui.

## Ce qui a été généré

- `AGYTEK-CodeSigning.pfx` : certificat **+ clé privée**, protégé par mot de passe. C'est le secret
  qui signe réellement les binaires — à ne jamais publier ni committer dans le dépôt.
- `AGYTEK-CodeSigning.cer` : certificat **public seul** (pas de clé privée). Sans danger à distribuer :
  c'est lui qu'on installe sur les postes AGYTEK pour qu'ils fassent confiance aux binaires signés
  avec le `.pfx` correspondant.
- Sujet : `CN=AGYTEK, O=AGYTEK, C=FR` — validité 10 ans à partir de la génération.

Ces deux fichiers ont été remis à l'utilisateur en dehors du dépôt (jamais commités en clair) ; le mot
de passe du `.pfx` a été communiqué séparément, en message, au moment de la génération.

## Mise en place (à faire une fois, manuellement — un jeton d'action GitHub ne peut pas créer de secrets)

1. Dans le dépôt GitHub `bysnoozy/AGY-Size` → **Settings → Secrets and variables → Actions →
   New repository secret**, créer :
   - `CODE_SIGNING_PFX_BASE64` : le contenu du fichier `AGYTEK-CodeSigning.pfx` encodé en base64
     (une seule ligne). Sous PowerShell :
     `[Convert]::ToBase64String([IO.File]::ReadAllBytes("AGYTEK-CodeSigning.pfx")) | Set-Clipboard`
     (le presse-papier contient alors la valeur à coller dans le secret).
   - `CODE_SIGNING_PASSWORD` : le mot de passe du `.pfx` (communiqué séparément).
2. Tant que ces deux secrets n'existent pas, la CI continue de fonctionner normalement et produit des
   binaires **non signés** (les étapes de signature se désactivent elles-mêmes, voir
   `scripts/Sign-Binaries.ps1`). Une fois les secrets créés, le prochain run signe automatiquement :
   - `AgySize.App.exe` et `AgySize.Cli.exe` (job `publish-windows` de `build.yml`, et
     `publish-release.yml`) ;
   - le binaire embarqué dans l'installeur, puis `AGY-Size-Setup.msi` lui-même (job `build-installer`
     de `build.yml`, et `publish-release.yml`).

## Installer le certificat de confiance sur un poste AGYTEK

Pour qu'un poste Windows fasse confiance aux binaires signés (plus d'avertissement SmartScreen lié à
la signature), installer `AGYTEK-CodeSigning.cer` dans le magasin **Éditeurs de confiance** de la
machine (nécessite des droits administrateur) :

```powershell
certutil -addstore "TrustedPublisher" AGYTEK-CodeSigning.cer
```

Ou via une stratégie de groupe (GPO) pour le déployer sur tous les postes AGYTEK d'un coup :
**Configuration ordinateur → Paramètres Windows → Paramètres de sécurité → Stratégies de clés
publiques → Éditeurs approuvés** → importer `AGYTEK-CodeSigning.cer`.

## Limites

- Un certificat auto-signé n'est reconnu que par les postes où `AGYTEK-CodeSigning.cer` a été
  installé explicitement ; il ne supprime pas l'avertissement Windows SmartScreen sur un poste
  extérieur à AGYTEK. C'est cohérent avec l'usage prévu : diffusion interne uniquement.
- Le `.pfx` généré avec OpenSSL 3.x utilise un chiffrement PBKDF2/AES-256 moderne. Si la signature
  échoue sur un poste Windows ancien (Server 2016 ou antérieur) avec une erreur liée au format PFX,
  régénérer le certificat avec l'option `-legacy` d'OpenSSL (compatibilité CryptoAPI historique).
- Le certificat expire dans 10 ans ; le renouveler avant l'échéance (même procédure de génération).
