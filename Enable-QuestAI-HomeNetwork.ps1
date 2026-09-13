$ErrorActionPreference = 'Stop'
$nodePath = (Get-Command node).Source
foreach ($rule in @(@{Name='QuestAI-Home-TCP';Protocol='TCP';Port=8787},@{Name='QuestAI-Home-Discovery';Protocol='UDP';Port=8786})) {
    if (-not (Get-NetFirewallRule -Name $rule.Name -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name $rule.Name -DisplayName $rule.Name -Direction Inbound -Action Allow -Protocol $rule.Protocol -LocalPort $rule.Port -Program $nodePath -Profile Private -InterfaceAlias 'Ethernet' -RemoteAddress LocalSubnet | Out-Null
    }
}
'Firewall Wi-Fi configurado.' | Set-Content 'C:\QuestIA\builds\wifi-firewall-result.txt'
