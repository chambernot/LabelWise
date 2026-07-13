@echo off
REM Start PostgreSQL with Docker
echo Starting PostgreSQL...
docker-compose up -d

REM Wait a moment for the database to start
timeout /t 5 /nobreak

echo PostgreSQL started successfully!
echo Connection string: Host=localhost;Port=5432;Database=labelwise_db;Username=labelwise_user;Password=changeme
pause
