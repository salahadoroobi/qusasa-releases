param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][uri]$InstallerUrl,
    [string]$ReleaseNotesPath = (Join-Path $PSScriptRoot 'release-notes.json'),
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$releaseVersion = [version]$Version
if (!$InstallerUrl.IsAbsoluteUri -or $InstallerUrl.Scheme -ne 'https' -or $InstallerUrl.UserInfo) { throw 'Installer URL must use public HTTPS.' }
$item = Get-Item -LiteralPath $Installer
if ($item.Extension -ne '.exe' -or $item.Length -lt 1 -or $item.Length -gt 250MB) { throw 'Use the generated Qusasa installer.' }
$actual = $item.VersionInfo.ProductVersion -split '\+', 2 | Select-Object -First 1
if ($actual -ne $releaseVersion.ToString()) { throw 'Installer product version does not match the manifest version.' }
$source = Get-Content -LiteralPath $ReleaseNotesPath -Raw | ConvertFrom-Json
if ($source.version -ne $releaseVersion.ToString() -or !$source.ar.Count -or !$source.en.Count) {
    throw 'Release notes must match the installer version and include both languages.'
}
$translations = @{}
foreach ($language in @('ar','en','ja','zh','fr','es')) {
    if (!$source.$language.Count) { throw "Missing release notes for $language." }
    $translations[$language] = ($source.$language | ForEach-Object { "• $($_.title)`n$($_.detail)" }) -join "`n`n"
    if ($translations[$language].Length -gt 12000) { throw 'Release notes exceed the supported length.' }
}
$payload = [Text.Encoding]::UTF8.GetBytes(([ordered]@{
    version=$releaseVersion.ToString()
    url=$InstallerUrl.AbsoluteUri
    sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $item.FullName).Hash
    size=$item.Length
    notesAr=$translations.ar
    notes=$translations.en
    localizedNotes=@{ja=$translations.ja;zh=$translations.zh;fr=$translations.fr;es=$translations.es}
} | ConvertTo-Json -Compress))
$keyPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'QusasaRelease/update-signing.dpapi'
$secret = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($keyPath), $null,
    [Security.Cryptography.DataProtectionScope]::CurrentUser)
$rsa = [Security.Cryptography.RSA]::Create()
try {
    $rsa.FromXmlString([Text.Encoding]::UTF8.GetString($secret))
    if ($rsa.ToXmlString($false) -ne (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'UpdateTrust.xml') -Raw).Trim()) {
        throw 'The signing key does not match the public key in the application.'
    }
    $signature = $rsa.SignData($payload, [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $signed = @{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($signature)} |
        ConvertTo-Json -Compress
    if ([Text.Encoding]::UTF8.GetByteCount($signed) -gt 96KB) { throw 'The update feed exceeds the supported length.' }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $signed)
} finally {
    [Array]::Clear($secret, 0, $secret.Length)
    $rsa.Dispose()
}
