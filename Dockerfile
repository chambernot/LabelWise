# 1. IMAGEM BASE OFICIAL
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
# Instalamos o ecossistema completo do OpenCV para garantir todos os codecs e gráficos
RUN apt-get update && apt-get install -y \
    libopencv-dev \
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
# Compilação sem o "-r" para que o .NET não delete os arquivos do Ubuntu
RUN dotnet publish "./LabelWise.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 🚀 A EXTRAÇÃO CIRÚRGICA: Tiramos o arquivo nativo do esconderijo e colocamos na raiz da aplicação
RUN cp /app/publish/runtimes/ubuntu.20.04-x64/native/libOpenCvSharpExtern.so /app/publish/ || true

# 4. FASE FINAL
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LabelWise.Api.dll"]