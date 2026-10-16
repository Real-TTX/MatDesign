# Copies MatPlatform into an app: the same as `node tools/matplatform-copy.js <folder>` (see there).
#   ./tools/matplatform-copy.ps1 -Target ..\MatTrakr\src\MatTrakr.Web [-Dry]
param(
    [Parameter(Mandatory = $true)][string]$Target,
    [switch]$Dry
)

$arguments = @("$PSScriptRoot/matplatform-copy.js", $Target)
if ($Dry) { $arguments += '--dry' }
& node @arguments
exit $LASTEXITCODE
