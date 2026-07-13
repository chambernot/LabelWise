# Fallback nutrition migration V2
$ErrorActionPreference = 'Stop'
$migrationPath = 'database-migrations'
$dbUser = 'postgres'
$dbName = 'labelwise_db'
$containerName = 'labelwise-postgres'

Write-Host '========================================'
Write-Host 'Fallback nutrition migration V2'
Write-Host '========================================'

$containers = docker ps --format '{{.Names}}'
if (-not ($containers -contains $containerName)) {
    Write-Host 'PostgreSQL container is not running.' -ForegroundColor Red
    exit 1
}

$createScript = Join-Path $migrationPath 'v2-create-nutrition-knowledge-base.sql'
$seedScript = Join-Path $migrationPath 'v2-seed-nutrition-knowledge-base.sql'

if (-not (Test-Path $createScript)) {
    Write-Host "Create script not found: $createScript" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $seedScript)) {
    Write-Host "Seed script not found: $seedScript" -ForegroundColor Red
    exit 1
}

Write-Host 'Applying create script...'
Get-Content $createScript | docker exec -i $containerName psql -U $dbUser -d $dbName
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Failed applying create script.' -ForegroundColor Red
    exit 1
}

Write-Host 'Applying seed script...'
Get-Content $seedScript | docker exec -i $containerName psql -U $dbUser -d $dbName
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Failed applying seed script.' -ForegroundColor Red
    exit 1
}

Write-Host 'Validating data...'
$counts = docker exec -i $containerName psql -U $dbUser -d $dbName -t -c 'SELECT ''nutrition_category'' AS table_name, COUNT(*) AS total FROM nutrition_category UNION ALL SELECT ''nutrition_category_profile'', COUNT(*) FROM nutrition_category_profile UNION ALL SELECT ''nutrition_category_alias'', COUNT(*) FROM nutrition_category_alias UNION ALL SELECT ''category_mappings'', COUNT(*) FROM category_mappings ORDER BY table_name;'
Write-Host $counts

docker exec -i $containerName psql -U $dbUser -d $dbName -c "SELECT * FROM v_category_summary LIMIT 1;" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Validation failed for v_category_summary.' -ForegroundColor Red
    exit 1
}

docker exec -i $containerName psql -U $dbUser -d $dbName -c "SELECT * FROM find_category_by_alias('cream cheese', 0.7);" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Validation failed for find_category_by_alias.' -ForegroundColor Red
    exit 1
}

Write-Host 'Fallback nutrition migration V2 applied successfully.' -ForegroundColor Green
