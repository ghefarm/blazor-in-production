# Lets ports 80 and 443 in only from the edge (Cloudflare), so nobody can
# reach the apps around it and write the visitor's header themselves.
# Run on the host as Administrator.
#
#   .\edge-only.ps1 -Settings <file>           shows what it would do
#   .\edge-only.ps1 -Settings <file> -Apply    adds the block rule
#   .\edge-only.ps1 -Rollback                  removes it again
#
# The ranges are read from "Edge:Ranges" in the API's settings file, so the
# firewall and the apps use one list. The rule is one BLOCK rule for every
# address outside them. In Windows Firewall a block rule wins over every
# allow rule, so no other rule is touched. Only local ports 80 and 443 are
# affected: remote desktop stays as it is, and loopback is never filtered.
param([string]$Settings, [switch]$Apply, [switch]$Rollback)
$ErrorActionPreference = 'Stop'

$RuleName = 'Block-Web-Not-Edge'

if ($Rollback) {
    Remove-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue
    Write-Host "Removed '$RuleName'."
    return
}

$ranges = @((Get-Content -Raw $Settings | ConvertFrom-Json).Edge.Ranges)
if ($ranges.Count -eq 0) {
    Write-Warning "No Edge.Ranges in $Settings. Nothing changed."
    return
}

# BigInteger, so IPv4 and IPv6 share one path. The bytes go in little-endian
# with a zero at the end, which keeps the number positive.
function ConvertTo-Number([string]$ip) {
    $bytes = [Net.IPAddress]::Parse($ip).GetAddressBytes()
    [Array]::Reverse($bytes)
    [Numerics.BigInteger]::new([byte[]]($bytes + [byte]0))
}

function ConvertTo-Ip([Numerics.BigInteger]$n, [int]$length) {
    $bytes = [byte[]]::new($length)
    $raw = $n.ToByteArray()
    [Array]::Copy($raw, $bytes, [Math]::Min($raw.Length, $length))
    [Array]::Reverse($bytes)
    [Net.IPAddress]::new($bytes).ToString()
}

# Everything NOT in the given blocks, as "first-last" ranges, which is the
# form the firewall accepts.
function Get-Gaps([string[]]$cidrs, [int]$bits) {
    $length = $bits / 8
    $blocks = $cidrs | ForEach-Object {
        $ip, $prefix = $_ -split '/'
        $start = ConvertTo-Number $ip
        $size = [Numerics.BigInteger]::Pow(2, $bits - [int]$prefix)
        [pscustomobject]@{ Start = $start; End = $start + $size - 1 }
    } | Sort-Object Start

    $next = [Numerics.BigInteger]::Zero
    $last = [Numerics.BigInteger]::Pow(2, $bits) - 1
    foreach ($b in $blocks) {
        if ($b.Start -gt $next) {
            '{0}-{1}' -f (ConvertTo-Ip $next $length),
                (ConvertTo-Ip ($b.Start - 1) $length)
        }
        if ($b.End + 1 -gt $next) { $next = $b.End + 1 }
    }
    if ($next -le $last) {
        '{0}-{1}' -f (ConvertTo-Ip $next $length), (ConvertTo-Ip $last $length)
    }
}

$v4 = @($ranges | Where-Object { -not $_.Contains(':') })
$v6 = @($ranges | Where-Object { $_.Contains(':') })
$outside = @(Get-Gaps $v4 32) + @(Get-Gaps $v6 128)
Write-Host "Would block TCP 80 and 443 from these $($outside.Count) ranges:"
$outside | ForEach-Object { Write-Host "  $_" }

if (-not $Apply) {
    Write-Host 'Nothing changed. Run again with -Apply.'
    return
}

# A rule only counts while the firewall is on. Switching it on from here
# could cut off remote desktop, so that is left to a person.
if (Get-NetFirewallProfile | Where-Object { -not $_.Enabled }) {
    Write-Warning 'The firewall is off in a profile. Nothing changed.'
    return
}

Remove-NetFirewallRule -Name $RuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -Name $RuleName `
    -DisplayName 'Block web (80, 443) unless from the edge' `
    -Direction Inbound -Action Block -Protocol TCP -LocalPort 80, 443 `
    -RemoteAddress $outside -Profile Any | Out-Null
Write-Host "Added '$RuleName'. Undo with -Rollback."
