# ============================================================
# 1. IMAGEM BASE
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

USER root

# Dependências nativas C++ e multimídia do Linux para o OpenCV no Debian 12
RUN apt-get update && apt-get install -y \
    libgl1 \
    libglib2.0-0 \
    libdrm2 \
    libgdiplus \
    libx11-xcb1 \
    libxcb-dri3-0 \
    libfontconfig1 \
    libgtk-3-0 \
    libavcodec-extra \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

WORKDIR /app

EXPOSE 8080
EXPOSE 8081

# ============================================================
# 2. BUILD
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# ------------------------------------------------------------
# Copia os .csproj
# ------------------------------------------------------------
COPY ["LabelWise.Api/LabelWise.Api.csproj", "LabelWise.Api/"]
COPY ["LabelWise.Application/LabelWise.Application.csproj", "LabelWise.Application/"]
COPY ["LabelWise.Domain/LabelWise.Domain.csproj", "LabelWise.Domain/"]
COPY ["LabelWise.Shared/LabelWise.Shared.csproj", "LabelWise.Shared/"]
COPY ["LabelWise.Infrastructure/LabelWise.Infrastructure.csproj", "LabelWise.Infrastructure/"]

# ------------------------------------------------------------
# Restore
# ------------------------------------------------------------
RUN dotnet restore "./LabelWise.Api/LabelWise.Api.csproj"

# ------------------------------------------------------------
# Copia código
# ------------------------------------------------------------
COPY . .

WORKDIR "/src/LabelWise.Api"

# ------------------------------------------------------------
# Build
# ------------------------------------------------------------
RUN dotnet build "./LabelWise.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore


# ============================================================
# 3. PUBLISH
# ============================================================
FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "./LabelWise.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore


# ============================================================
# 4. VALIDAÇÃO DO OPEN CV
# ============================================================
RUN echo "===== Verificando arquivos OpenCvSharp =====" && \
    find /app/publish -iname "*OpenCvSharp*" -o -iname "libOpenCvSharpExtern.so"


# ============================================================
# 5. IMAGEM FINAL
# ============================================================
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

USER root
# Garante que a DLL nativa seja encontrada mesmo se a busca do .NET falhar em subpastas
RUN cp /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so /app/ 2>/dev/null || true
USER $APP_UID

ENTRYPOINT ["dotnet", "LabelWise.Api.dll"]