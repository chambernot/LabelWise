using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Infrastructure.Storage;

namespace LabelWise.Infrastructure.Services
{
    public class ImageUploadService : IImageUploadService
    {
        private readonly IFileStorage _storage;
        private static readonly string[] _allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public ImageUploadService(IFileStorage storage)
        {
            _storage = storage;
        }

        public async Task<ImageUploadResultDto> UploadImageAsync(Stream imageStream, string fileName)
        {
            var result = new ImageUploadResultDto
            {
                FileName = fileName,
                UploadedAt = DateTime.UtcNow
            };

            try
            {
                // Validação
                if (!ValidateImage(fileName, imageStream.Length))
                {
                    result.Success = false;
                    result.ErrorMessage = "Formato de arquivo inválido ou tamanho excede o limite (5MB)";
                    return result;
                }

                // Upload
                var path = await _storage.SaveTempAsync(imageStream, fileName);

                result.ImagePath = path;
                result.ContentType = GetContentType(fileName);
                result.FileSize = imageStream.Length;
                result.Success = true;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Erro no upload: {ex.Message}";
                return result;
            }
        }

        public bool ValidateImage(string fileName, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !_allowedExtensions.Contains(ext))
                return false;

            if (fileSize <= 0 || fileSize > MaxFileSize)
                return false;

            return true;
        }

        private string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }
    }
}
