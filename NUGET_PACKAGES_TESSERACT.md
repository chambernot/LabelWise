# 📦 Pacotes NuGet Necessários para Tesseract OCR

## Pacote Principal

### **Tesseract**
- **Nome:** `Tesseract`
- **Autor:** Charlesw
- **Versão Recomendada:** `5.2.0` ou superior
- **Link:** https://www.nuget.org/packages/Tesseract/

## Instalação

### Via Package Manager Console (Visual Studio)
```powershell
Install-Package Tesseract -Version 5.2.0 -ProjectName LabelWise.Infrastructure
```

### Via .NET CLI
```bash
dotnet add LabelWise.Infrastructure package Tesseract --version 5.2.0
```

### Via NuGet Package Manager (GUI)
1. Clique com botão direito em `LabelWise.Infrastructure`
2. Selecione **Manage NuGet Packages**
3. Busque por: `Tesseract`
4. Selecione o pacote de **Charlesw**
5. Instale a versão `5.2.0` ou superior

## Dependências Automáticas

O pacote Tesseract instala automaticamente suas dependências nativas:

- **Tesseract OCR Engine** (binários nativos)
- **Leptonica** (processamento de imagens)
- Bibliotecas C++ Runtime necessárias

## ⚠️ Importante

### Pacote CORRETO:
✅ `Tesseract` by Charlesw

### Pacotes INCORRETOS (evite):
❌ `TesseractOCR`
❌ `Tesseract.Net`
❌ `TesseractSharp`

## Verificar Instalação

Após instalar, verifique se o pacote está no `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Tesseract" Version="5.2.0" />
</ItemGroup>
```

## Arquivos Nativos

O pacote copia automaticamente os binários nativos para:
```
bin\Debug\net10.0\
├── leptonica-1.82.0.dll
├── tesseract50.dll
└── x64\
    └── (outros binários)
```

## Troubleshooting

### Erro: "Could not load file or assembly 'Tesseract'"
**Solução:** Limpe e recompile o projeto
```bash
dotnet clean
dotnet build
```

### Erro: "DllNotFoundException: liblept"
**Solução:** Reinstale o pacote NuGet
```bash
dotnet remove LabelWise.Infrastructure package Tesseract
dotnet add LabelWise.Infrastructure package Tesseract --version 5.2.0
```

### Erro: "Platform not supported"
**Solução:** Certifique-se de estar usando Windows x64 ou configure para a plataforma correta

## Próximos Passos

Após instalar o pacote, siga o guia completo:
👉 **TESSERACT_INSTALLATION_GUIDE.md**

Ou execute o script de setup automático:
```powershell
.\setup-tesseract.ps1
```
