using LabelWise.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Application.Interfaces
{
    public interface IGeminiService
    {
        // Método atualizado para suportar múltiplas imagens do mesmo produto
        Task<string> AnalyzeMultipleImagesAsync(string prompt, List<ImageAttachmentDto> images);

        // Mantido para compatibilidade com o Chat antigo
        Task<string> SendMessageToChatAsync(string prompt, byte[]? bytes, string? mimeType);
    }
}
