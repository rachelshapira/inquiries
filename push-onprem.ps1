# push-onprem.ps1
# Push the Workflow system to GitHub, then clone it on any on-prem environment.
# Run from the project folder:   .\push-onprem.ps1
# (If scripts are blocked:        powershell -ExecutionPolicy Bypass -File .\push-onprem.ps1 )
#
# Safety: shows what will be pushed and asks before committing/pushing.

$ErrorActionPreference = "Stop"
$RepoUrl = "https://github.com/rachelshapira/inquiries.git"
$Branch  = "main"

Write-Host "== Workflow system -> GitHub ==" -ForegroundColor Cyan
Write-Host "Target: $RepoUrl ($Branch)`n"

# 1. init repo if needed
if (-not (Test-Path .git)) {
    git init
    git branch -M $Branch
}

# 2. wire remote (add or update)
$existing = git remote 2>$null
if ($existing -contains "origin") {
    git remote set-url origin $RepoUrl
} else {
    git remote add origin $RepoUrl
}

# 3. stage
git add .

# 4. show what will be pushed
Write-Host "`n--- Files staged (sample) ---" -ForegroundColor Yellow
git status --short

# 5. warn on sensitive files
$danger = git status --short | Select-String -Pattern "App_Data|\.env|appsettings\..*\.json|secrets|\.pfx|\.key"
if ($danger) {
    Write-Host "`nWARNING - files that may not belong in git:" -ForegroundColor Red
    $danger
    Write-Host "If these are secrets or the local DB: press Ctrl+C, add them to .gitignore, run again." -ForegroundColor Red
}

# 6. confirm before commit + push
$answer = Read-Host "Continue to commit and push? (yes/no)"
if ($answer -ne "yes") {
    Write-Host "Cancelled. Nothing was pushed." -ForegroundColor Yellow
    exit
}

# 7. commit only if there are changes
$pending = git status --porcelain
if ($pending) {
    git commit -m "Workflow platform: engine + outbox + rabbitmq dispatch"
} else {
    Write-Host "No new changes to commit." -ForegroundColor Yellow
}

# 8. push
git push -u origin $Branch

Write-Host "`nDone. Repo: $RepoUrl" -ForegroundColor Green
Write-Host "On the on-prem side:  git clone $RepoUrl" -ForegroundColor Green
