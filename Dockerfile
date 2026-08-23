# 1. FASE BASE: Instalando as dependências gráficas corretas do Debian
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update && apt-get install -y \
    libgl1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    libgomp1 \
    libstdc++6 \
    zlib1g \
    libc6 \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# 2. FASE DE BUILD E RESTAURAÇÃO
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["LabelWise.Api/LabelWise.Api.csproj", "LabelWise.Api/"]
COPY ["LabelWise.Application/LabelWise.Application.csproj", "LabelWise.Application/"]
COPY ["LabelWise.Domain/LabelWise.Domain.csproj", "LabelWise.Domain/"]
COPY ["LabelWise.Shared/LabelWise.Shared.csproj", "LabelWise.Shared/"]
COPY ["LabelWise.Infrastructure/LabelWise.Infrastructure.csproj", "LabelWise.Infrastructure/"]
RUN dotnet restore "./LabelWise.Api/LabelWise.Api.csproj"
COPY . .
WORKDIR "/src/LabelWise.Api"
RUN dotnet build "./LabelWise.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 3. FASE DE PUBLICAÇÃO
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Compilamos para o Linux genérico
RUN dotnet publish "./LabelWise.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish -r linux-x64 --self-contained false /p:UseAppHost=false

# 🚀 A MÁGICA ACONTECE AQUI:
# Buscamos o arquivo nativo no cache do NuGet e forçamos a cópia para a pasta de publicação
RUN find /root/.nuget/packages/ -name "libOpenCvSharpExtern.so" -exec cp {} /app/publish/ \;

# 4. FASE FINAL
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LabelWise.Api.dll"]