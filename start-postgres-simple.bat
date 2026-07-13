@echo off
echo ========================================
echo  PostgreSQL Setup - LabelWise
echo ========================================
echo.

echo [1/3] Parando containers antigos...
docker compose down 2>nul

echo.
echo [2/3] Iniciando PostgreSQL...
docker compose up -d

echo.
echo [3/3] Aguardando PostgreSQL inicializar...
timeout /t 5 /nobreak >nul

echo.
echo ========================================
echo  Verificando Status...
echo ========================================
docker ps --filter "ancestor=postgres"

echo.
echo ========================================
echo  Testando Conexao...
echo ========================================

for /f "tokens=*" %%i in ('docker ps --filter "ancestor=postgres" --format "{{.Names}}" --quiet') do (
    echo Container encontrado: %%i
    docker exec %%i psql -U labelwise_user -d labelwise_db -c "SELECT version();"
    
    if errorlevel 1 (
        echo [ERRO] Falha ao conectar no PostgreSQL
    ) else (
        echo.
        echo ========================================
        echo  PostgreSQL PRONTO!
        echo ========================================
        echo.
        echo Informacoes de Conexao:
        echo   Host:     localhost
        echo   Porta:    5432
        echo   Database: labelwise_db
        echo   Usuario:  labelwise_user
        echo   Senha:    changeme
        echo.
        echo Proximo passo:
        echo   dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api
    )
)

echo.
pause
