param([string]$Compiler = "ISCC.exe", [string]$ReferenceRoot,
      [string]$UpdateFeedUrl = "https://github.com/salahadoroobi/qusasa-releases/releases/latest/download/stable.json",
      [string]$OcrRelayUrl = "https://qusasa-ocr-relay.chirpydate4.chatgpt.site/v1/scan",
      [string]$SignTool, [string]$SigningThumbprint,
      [string]$TimestampUrl = "http://timestamp.digicert.com")
$ErrorActionPreference = "Stop"
if ([bool]$SignTool -ne [bool]$SigningThumbprint) {
    throw "Specify both SignTool and the trusted code-signing certificate thumbprint."
}
function Sign-Binary([string]$Path) {
    if (!$SignTool) { return }
    & $SignTool sign /sha1 $SigningThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
    if ($LASTEXITCODE -or (Get-AuthenticodeSignature -LiteralPath $Path).Status -ne 'Valid') {
        throw "Authenticode signature is not trusted: $Path"
    }
}
Push-Location $PSScriptRoot
try {
    $build = @("-c", "Release", "--nologo")
    $notes = Get-Content -LiteralPath release-notes.json -Raw | ConvertFrom-Json
    $project = [xml](Get-Content -LiteralPath Qusasa.csproj -Raw)
    $version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ($notes.version -ne $version) { throw "Update release-notes.json to $version." }
    foreach ($language in @('ar','en','ja','zh','fr','es')) {
        $items = $notes.$language
        if (!$items.Count) { throw "Missing release notes for $language." }
        foreach ($item in $items) {
            if ([string]::IsNullOrWhiteSpace($item.title) -or [string]::IsNullOrWhiteSpace($item.detail)) {
                throw "Every release note needs a title and detail."
            }
        }
    }
    if ($UpdateFeedUrl) {
        $feed = [Uri]$UpdateFeedUrl
        if (!$feed.IsAbsoluteUri -or $feed.Scheme -ne "https" -or $feed.UserInfo -or $feed.Query -or $feed.Fragment) {
            throw "Use a public HTTPS update feed with no credentials, query, or fragment."
        }
        $build += "-p:UpdateFeedUrl=$($feed.AbsoluteUri)"
    }
    if ($OcrRelayUrl) {
        $endpoint = [Uri]$OcrRelayUrl
        if (!$endpoint.IsAbsoluteUri -or $endpoint.Scheme -ne "https" -or $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment) {
            throw "Use the public HTTPS endpoint with no credentials, query, or fragment."
        }
        $build += "-p:OcrRelayUrl=$($endpoint.AbsoluteUri)"
    }
    if ($ReferenceRoot) {
        $build += "-p:TargetFrameworkRootPath=$ReferenceRoot"
        $build += "-p:AutomaticallyUseReferenceAssemblyPackages=false"
    }
    dotnet build Qusasa.csproj @build
    if ($LASTEXITCODE) { throw "Application build failed." }
    Sign-Binary "bin/Release/net48/Qusasa.exe"
    & $Compiler /Q Qusasa.iss
    if ($LASTEXITCODE) { throw "Installer engine build failed." }
    $payload = (Resolve-Path "bin/Installer/Qusasa-1.28-Engine.exe").Path
    Sign-Binary $payload
    dotnet build Qusasa.csproj @build -p:SetupFrontend=true "-p:SetupPayload=$payload" -o bin/Installer/frontend
    if ($LASTEXITCODE) { throw "Installer interface build failed." }
    Sign-Binary "bin/Installer/frontend/Qusasa.Setup.exe"
}
finally { Pop-Location }
