param([string]$SecretsDirectory)
$ErrorActionPreference = 'Stop'
if (!$SecretsDirectory) {
    $workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
    $SecretsDirectory = Join-Path $workspaceRoot 'work/rabbitmq-secrets'
}
$values = @{}
Get-Content -LiteralPath (Join-Path $SecretsDirectory '.env') | ForEach-Object {
    if ($_ -match '^(RABBITMQ_USER|RABBITMQ_PASSWORD)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
}
if (!$values['RABBITMQ_USER'] -or !$values['RABBITMQ_PASSWORD']) { throw 'Local RabbitMQ credentials are missing.' }
$previousUser = $env:RABBITMQ_USER
$previousPassword = $env:RABBITMQ_PASSWORD
try {
    $env:RABBITMQ_USER = $values['RABBITMQ_USER']
    $env:RABBITMQ_PASSWORD = $values['RABBITMQ_PASSWORD']
    & dotnet run --project (Join-Path $PSScriptRoot 'RabbitMq.Checks.csproj') --no-restore --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ acceptance checks failed.' }
}
finally {
    $env:RABBITMQ_USER = $previousUser
    $env:RABBITMQ_PASSWORD = $previousPassword
}
