param(
    [string]$AuditLogPath = "ApexPost.Web\App_Data\audit-log.jsonl",
    [string]$OutputPath = "AUDIT-SAMPLE.json"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AuditLogPath -PathType Leaf)) {
    throw "Audit log not found: $AuditLogPath. Run the application and complete audited actions first."
}

$safeEntries = [System.Collections.Generic.List[object]]::new()
$allowedParameters = @(
    "controller",
    "action",
    "transactionId",
    "branchCode",
    "counterId",
    "productSku",
    "quantityDelta",
    "reasonCode",
    "businessAction",
    "outcome",
    "retailLines",
    "shipments",
    "moneyOrders",
    "documentType",
    "verified",
    "moneyOrderAmount",
    "identityVerification",
    "paymentMethod",
    "id"
)

foreach ($line in Get-Content -LiteralPath $AuditLogPath) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $entry = $line | ConvertFrom-Json
    $parameters = @{}
    if (-not [string]::IsNullOrWhiteSpace($entry.Parameters)) {
        $rawParameters = $entry.Parameters | ConvertFrom-Json
        foreach ($name in $allowedParameters) {
            $property = $rawParameters.PSObject.Properties[$name]
            if ($null -ne $property -and "$($property.Value)" -match '^[A-Za-z0-9_.-]{1,128}$') {
                $parameters[$name] = "$($property.Value)"
            }
        }
    }

    $safeEntries.Add([ordered]@{
        Id = $entry.Id
        CorrelationId = $entry.CorrelationId
        StaffId = $entry.StaffId
        Role = $entry.Role
        BranchCode = $entry.BranchCode
        Controller = $entry.Controller
        Action = $entry.Action
        Parameters = ($parameters | ConvertTo-Json -Compress)
        StatusCode = $entry.StatusCode
        DurationMs = $entry.DurationMs
        TimestampUtc = $entry.TimestampUtc
    })
}

if ($safeEntries.Count -eq 0) {
    throw "No audit entries were found in $AuditLogPath."
}

$destination = [System.IO.Path]::GetFullPath($OutputPath)
$parent = [System.IO.Path]::GetDirectoryName($destination)
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$safeEntries |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $destination -Encoding utf8

Write-Output "Exported $($safeEntries.Count) filtered audit entries to $destination"
