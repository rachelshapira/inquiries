param([string]$SecretsDirectory, [switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
if (!$SecretsDirectory) {
    $workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
    $SecretsDirectory = Join-Path $workspaceRoot 'work/rabbitmq-secrets'
}
New-Item -ItemType Directory -Path $SecretsDirectory -Force | Out-Null
$environmentFile = Join-Path $SecretsDirectory '.env'
if (!(Test-Path -LiteralPath $environmentFile)) {
    $password = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    @("RABBITMQ_USER=workflow-demo", "RABBITMQ_PASSWORD=$password", 'RABBITMQ_IMAGE=rabbitmq:4.2-management') |
        Set-Content -LiteralPath $environmentFile -Encoding utf8
}
& docker compose --env-file $environmentFile -f (Join-Path $PSScriptRoot 'compose.yaml') config --quiet
if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ compose configuration is invalid.' }
if ($PrepareOnly) { Write-Output 'RabbitMQ compose and local secrets prepared; no container started.'; return }
& docker compose --env-file $environmentFile -f (Join-Path $PSScriptRoot 'compose.yaml') up -d
if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ startup failed; verify Docker Engine and image access.' }
Write-Output 'RabbitMQ demo started. Credentials remain in the local secrets file; they are not printed.'
Write-Output 'AMQP: 127.0.0.1:5672, virtual host: workflow-demo. Management: http://127.0.0.1:15672'
