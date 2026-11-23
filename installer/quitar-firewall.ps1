# Desinstalación: quita las reglas que creó firewall.ps1 (se ejecuta elevado).
Get-NetFirewallRule -DisplayName "ModoSofá*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
