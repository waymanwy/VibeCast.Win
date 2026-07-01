# Allow VibeCast to accept inbound connections from your LAN (phone).
# Run in an ELEVATED PowerShell:  Right-click PowerShell -> Run as administrator
#   powershell -ExecutionPolicy Bypass -File .\add-firewall-rule.ps1
#
# Opens VibeCast's HTTP port (default 8443) for Private networks only.

param(
    [int]$Port = 8443
)

$ruleName = "VibeCast (VoiceCast) TCP $Port"

$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Rule already exists: $ruleName"
    return
}

New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port `
    -Profile Private `
    -Description "Allow phones on the local network to reach VibeCast." | Out-Null

Write-Host "Added firewall rule '$ruleName' for TCP $Port (Private profile)."
Write-Host "Remove later with: Remove-NetFirewallRule -DisplayName '$ruleName'"
