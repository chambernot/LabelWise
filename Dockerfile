# Acesse https://aka.ms/customizecontainer para saber como personalizar seu contêiner de depuração e como o Visual Studio usa este Dockerfile para criar suas imagens para uma depuração mais rápida.

# Esta fase é usada durante a execução no VS no modo rápido
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root

# Instalando dependências com os nomes corretos para o Debian/Ubuntu moderno
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
# Esta fase é usada para compilar o projeto de serviço
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

# Esta fase é usada para publicar o projeto de serviço a ser copiado para a fase final
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# ADICIONADO O "-r linux-x64 --self-contained false" AQUI PARA FORÇAR A CÓPIA DOS BINÁRIOS NATIVOS DO UBUNTU:
RUN dotnet publish "./LabelWise.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish -r linux-x64 --self-contained false /p:UseAppHost=false

# Esta fase é usada na produção ou quando executada no VS no modo normal (padrão quando não está usando a configuração de Depuração)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LabelWise.Api.dll"]