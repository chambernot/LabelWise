using LabelWise.Application.DTOs; // Onde está o seu ImageAttachmentDto
using LabelWise.Application.Interfaces; // Certifique-se de que a IGeminiService esteja aqui
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly GenerativeModel _model;
        private readonly List<Content> _chatHistory;

        public GeminiService(string apiKey)
        {
            var googleAi = new GoogleAI(apiKey: apiKey);

            // Mantido o modelo de ponta que você configurou
            _model = googleAi.GenerativeModel("gemini-3-flash-preview");

            // Inicializa o histórico para as funções de chat
            _chatHistory = new List<Content>();
        }

        // --- NOVO: MÉTODO PARA EXTRAÇÃO DE RÓTULOS (SEM ESTADO) ---
        public async Task<string> AnalyzeMultipleImagesAsync(string prompt, List<ImageAttachmentDto> images)
        {
            var parts = new List<IPart>();

            // 1. Adiciona todas as imagens passadas na lista
            foreach (var img in images)
            {
                if (img.Bytes != null && img.Bytes.Length > 0 && !string.IsNullOrEmpty(img.MimeType))
                {
                    parts.Add(new InlineData
                    {
                        MimeType = img.MimeType,
                        Data = Convert.ToBase64String(img.Bytes)
                    });
                }
            }

            // 2. Adiciona o texto do prompt
            if (!string.IsNullOrEmpty(prompt))
            {
                parts.Add(new TextData
                {
                    Text = prompt
                });
            }

            // 3. Monta a requisição isolada (sem adicionar ao _chatHistory)
            var request = new GenerateContentRequest
            {
                Contents = new List<Content>
                {
                    new Content { Role = Role.User, Parts = parts }
                },
                GenerationConfig = new GenerationConfig
                {
                    Temperature = 0.0f, // Fundamental para não haver "alucinação" ao extrair a tabela nutricional
                    ResponseMimeType = "application/json"
                }
            };

            var response = await _model.GenerateContent(request);

            return response.Text ?? "{}";
        }

        // --- MANTIDO: SEU MÉTODO ORIGINAL DE CHAT (COM ESTADO) ---
        public async Task<string> SendMessageToChatAsync(string textPrompt, byte[]? imageBytes, string? mimeType)
        {
            var parts = new List<IPart>();

            if (imageBytes != null && !string.IsNullOrEmpty(mimeType))
            {
                parts.Add(new InlineData
                {
                    MimeType = mimeType,
                    Data = Convert.ToBase64String(imageBytes)
                });
            }

            if (!string.IsNullOrEmpty(textPrompt))
            {
                parts.Add(new TextData
                {
                    Text = textPrompt
                });
            }

            // Grava a pergunta do usuário no histórico
            _chatHistory.Add(new Content
            {
                Role = Role.User,
                Parts = parts
            });

            var request = new GenerateContentRequest
            {
                Contents = _chatHistory,
                GenerationConfig = new GenerationConfig
                {
                    ResponseMimeType = "application/json"
                }
            };

            var response = await _model.GenerateContent(request);

            // Grava a resposta da IA no histórico
            if (!string.IsNullOrEmpty(response.Text))
            {
                _chatHistory.Add(new Content
                {
                    Role = Role.Model,
                    Parts = new List<IPart> { new TextData { Text = response.Text } }
                });
            }

            return response.Text ?? "{}";
        }
    }
}