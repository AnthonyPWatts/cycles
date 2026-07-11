[CmdletBinding()]
param(
    [int] $Port = 5087,
    [string] $StatePath = (Join-Path ([System.IO.Path]::GetTempPath()) "cycles-alpha-smoke-$([Guid]::NewGuid().ToString('N')).json"),
    [string] $Configuration = "Debug",
    [switch] $NoBuild,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = "Stop"

function Assert-Condition {
    param(
        [bool] $Condition,
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-Json {
    param(
        [System.Net.Http.HttpClient] $Client,
        [string] $Path
    )

    $response = $Client.GetAsync($Path).GetAwaiter().GetResult()
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        throw "GET $Path failed with HTTP $([int]$response.StatusCode): $content"
    }

    return $content | ConvertFrom-Json
}

function Post-Json {
    param(
        [System.Net.Http.HttpClient] $Client,
        [string] $Path,
        [hashtable] $Body
    )

    $json = $Body | ConvertTo-Json -Compress
    $requestContent = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, "application/json")
    $response = $Client.PostAsync($Path, $requestContent).GetAwaiter().GetResult()
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        throw "POST $Path failed with HTTP $([int]$response.StatusCode): $content"
    }

    return $content | ConvertFrom-Json
}

function New-SessionClient {
    param([Uri] $BaseAddress)

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $client = [System.Net.Http.HttpClient]::new($handler, $true)
    $client.BaseAddress = $BaseAddress
    return $client
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$stateFullPath = [System.IO.Path]::GetFullPath($StatePath)
if (Test-Path -LiteralPath $stateFullPath) {
    throw "Alpha smoke state path already exists: $stateFullPath"
}

$artifactDirectory = Split-Path -Parent $stateFullPath
[System.IO.Directory]::CreateDirectory($artifactDirectory) | Out-Null
$stdoutPath = "$stateFullPath.api.stdout.log"
$stderrPath = "$stateFullPath.api.stderr.log"
$baseAddress = [Uri]"http://127.0.0.1:$Port"
$runArguments = @("run", "--configuration", $Configuration)
if ($NoBuild) {
    $runArguments += "--no-build"
}
$apiProject = Join-Path $repoRoot "src/Cycles.Api/Cycles.Api.csproj"
$apiAssembly = Join-Path $repoRoot "src/Cycles.Api/bin/$Configuration/net8.0/Cycles.Api.dll"

$apiProcess = $null
$playerClient = $null

try {
    $seedArguments = $runArguments + @(
        "--project", (Join-Path $repoRoot "src/Cycles.Cli"),
        "--", "seed", $stateFullPath, "8", "2", "90210"
    )
    & dotnet @seedArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to seed the alpha smoke state."
    }

    if (-not $NoBuild) {
        & dotnet build $apiProject --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build the API for the alpha smoke journey."
        }
    }
    Assert-Condition (Test-Path -LiteralPath $apiAssembly) "The built API assembly was not found at $apiAssembly."

    $apiArguments = @(
        $apiAssembly,
        "--environment", "Development",
        "--urls", $baseAddress.AbsoluteUri.TrimEnd('/'),
        "--Cycles:StatePath", $stateFullPath
    )
    $apiProcess = Start-Process dotnet `
        -ArgumentList $apiArguments `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru

    $healthClient = [System.Net.Http.HttpClient]::new()
    $healthClient.BaseAddress = $baseAddress
    try {
        $started = $false
        for ($attempt = 1; $attempt -le 30; $attempt++) {
            if ($apiProcess.HasExited) {
                break
            }

            try {
                $health = $healthClient.GetAsync("/health").GetAwaiter().GetResult()
                if ($health.IsSuccessStatusCode) {
                    $started = $true
                    break
                }
            }
            catch [System.Net.Http.HttpRequestException] {
                # The API has not bound its port yet.
            }

            Start-Sleep -Milliseconds 500
        }

        Assert-Condition $started "The API did not become healthy at $baseAddress."
    }
    finally {
        $healthClient.Dispose()
    }

    $playerClient = New-SessionClient $baseAddress

    $login = Post-Json $playerClient "/auth/login" @{
        username = "player-1"
        empireName = $null
        isAdmin = $false
    }
    Assert-Condition ($login.role -eq "player") "Expected player-1 to receive the player role."
    Assert-Condition $login.canAdvanceTurn "Expected a Development player to receive the advance-turn capability."

    $cycleBefore = Get-Json $playerClient "/cycles/current"
    $empireBefore = Get-Json $playerClient "/empire"
    $fleetsBefore = @(Get-Json $playerClient "/fleets")
    $galaxy = Get-Json $playerClient "/galaxy"
    $fleet = $fleetsBefore[0].fleet
    Assert-Condition ($null -ne $fleet) "The alpha player did not receive a fleet."

    $link = @($galaxy.links | Where-Object {
        $_.systemAId -eq $fleet.currentSystemId -or $_.systemBId -eq $fleet.currentSystemId
    } | Select-Object -First 1)[0]
    Assert-Condition ($null -ne $link) "The player's home system has no linked destination."
    $targetSystemId = if ($link.systemAId -eq $fleet.currentSystemId) { $link.systemBId } else { $link.systemAId }

    $priorities = Post-Json $playerClient "/orders/priorities" @{
        industryWeight = 20
        researchWeight = 20
        militaryWeight = 40
        expansionWeight = 20
    }
    Assert-Condition ($priorities.militaryWeight -eq 40) "The API did not save the alpha player's priorities."

    $move = Post-Json $playerClient "/orders/fleet/move" @{
        fleetId = $fleet.fleetId
        targetSystemId = $targetSystemId
    }
    Assert-Condition ($move.status -eq "pending") "The move order was not pending after submission."
    Assert-Condition ($move.executeAfterTick -eq ($cycleBefore.currentTickNumber + 1)) "The move order targeted the wrong execution tick."

    $pendingOrders = @(Get-Json $playerClient "/orders")
    Assert-Condition ($pendingOrders.fleetOrderId -contains $move.fleetOrderId) "The pending order was not visible to its player."

    $tick = Post-Json $playerClient "/admin/tick" @{}
    Assert-Condition ($tick.status -eq "completed") "The player's Development tick did not complete."
    Assert-Condition ($tick.tickNumber -eq ($cycleBefore.currentTickNumber + 1)) "The Development tick advanced to an unexpected number."

    $ordersAfter = @(Get-Json $playerClient "/orders")
    $processedMove = $ordersAfter | Where-Object { $_.fleetOrderId -eq $move.fleetOrderId } | Select-Object -First 1
    Assert-Condition ($processedMove.status -eq "processed") "The player's move order was not processed by the tick."

    $fleetsAfter = @(Get-Json $playerClient "/fleets")
    $fleetAfter = ($fleetsAfter | Where-Object { $_.fleet.fleetId -eq $fleet.fleetId } | Select-Object -First 1).fleet
    $reachedOrTravelling = $fleetAfter.currentSystemId -eq $targetSystemId `
        -or $fleetAfter.destinationSystemId -eq $targetSystemId
    Assert-Condition $reachedOrTravelling "The fleet neither reached nor began travelling to its ordered destination."

    $empireAfter = Get-Json $playerClient "/empire"
    Assert-Condition ($empireAfter.priorities.militaryWeight -eq 40) "The saved priority was not visible after the tick."
    Assert-Condition ($empireAfter.resources.lastGeneratedIndustry -gt 0) "The tick did not generate visible industry for the player."

    $events = @(Get-Json $playerClient "/events/recent?limit=50")
    Assert-Condition ($events.eventType -contains "prioritiesChanged") "The priority-change event was not visible to the player."
    Assert-Condition ($events.eventType -contains "fleetMoved") "The fleet-movement event was not visible to the player."

    Write-Host "Alpha gameplay journey passed: player login, priorities, pending move, Development turn advancement, processed movement, resources, and events."
}
catch {
    if (Test-Path -LiteralPath $stdoutPath) {
        Write-Host "--- API stdout ---"
        Get-Content -LiteralPath $stdoutPath
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Host "--- API stderr ---"
        Get-Content -LiteralPath $stderrPath
    }
    throw
}
finally {
    if ($null -ne $playerClient) {
        $playerClient.Dispose()
    }

    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }
    if ($null -ne $apiProcess) {
        $apiProcess.Dispose()
    }

    if (-not $KeepArtifacts) {
        Remove-Item -LiteralPath $stateFullPath, $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "Alpha smoke artifacts retained at $stateFullPath and adjacent log files."
    }
}
