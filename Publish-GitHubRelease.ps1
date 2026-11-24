param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$Feed
)
$ErrorActionPreference = 'Stop'
$repo = 'salahadoroobi/qusasa-releases'
$api = "https://api.github.com/repos/$repo"
$exe = Get-Item -LiteralPath $Installer
$manifest = Get-Item -LiteralPath $Feed
$envelope = Get-Content -LiteralPath $manifest.FullName -Raw | ConvertFrom-Json
$payload = [Convert]::FromBase64String($envelope.payload)
$rsa = [Security.Cryptography.RSA]::Create()
try {
    $rsa.FromXmlString((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'UpdateTrust.xml') -Raw))
    if (!$rsa.VerifyData($payload, [Convert]::FromBase64String($envelope.signature),
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)) {
        throw 'The update manifest signature is invalid.'
    }
} finally { $rsa.Dispose() }

$metadata = [Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
$version = [version]$metadata.version
$tag = "v$version"
$assetUrl = "https://github.com/$repo/releases/download/$tag/$($exe.Name)"
if ($metadata.url -ne $assetUrl -or $metadata.size -ne $exe.Length -or
    $metadata.sha256 -ne (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash) {
    throw 'The installer does not match the signed update manifest.'
}

$env:GCM_INTERACTIVE = 'Never'
$env:GIT_TERMINAL_PROMPT = '0'
$credential = @('protocol=https','host=github.com','') | git credential fill
$entry = $credential -match '^password=' | Select-Object -First 1
if (!$entry) { throw 'No GitHub credential in Git Credential Manager.' }
$headers = @{
    Authorization = 'Bearer ' + $entry.Substring(9)
    'User-Agent' = 'Qusasa Release'
    Accept = 'application/vnd.github+json'
}
if ((Invoke-RestMethod https://api.github.com/user -Headers $headers).login -ne 'salahadoroobi') {
    throw 'Sign in with the Qusasa publisher account.'
}

$releases = Invoke-RestMethod "$api/releases?per_page=100" -Headers $headers
$release = @($releases | Where-Object tag_name -EQ $tag) | Select-Object -First 1
if (!$release) {
    $body = @{
        tag_name = $tag
        name = "Qusasa $($version.ToString(2))"
        draft = $true
        prerelease = $false
        body = "$($metadata.notesAr)`n`n---`n`n$($metadata.notes)"
    } | ConvertTo-Json -Compress
    $release = Invoke-RestMethod "$api/releases" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
}

function Ensure-Asset($item, [string]$name, [string]$type) {
    $expected = 'sha256:' + (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $asset = @($release.assets | Where-Object name -EQ $name) | Select-Object -First 1
    if (!$asset) {
        if (!$release.draft) { throw "The published release is missing $name." }
        $upload = ($release.upload_url -split '\{', 2)[0] + '?name=' + [uri]::EscapeDataString($name)
        if (([uri]$upload).Host -ne 'uploads.github.com') { throw 'Unexpected release upload host.' }
        $asset = Invoke-RestMethod $upload -Method Post -Headers $headers -ContentType $type -InFile $item.FullName -TimeoutSec 240
    }
    if ($asset.size -ne $item.Length -or $asset.digest -ne $expected) { throw "GitHub asset $name failed validation." }
}
Ensure-Asset $exe $exe.Name 'application/octet-stream'
Ensure-Asset $manifest 'stable.json' 'application/json'
if ($release.draft) {
    $release = Invoke-RestMethod "$api/releases/$($release.id)" -Method Patch -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}'
}
$release | Select-Object html_url, tag_name, draft
