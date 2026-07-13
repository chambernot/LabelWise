using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Serviço responsável pelo upload e validação de imagens.
    /// </summary>
    public interface IImageUploadService
    {
        /// <summary>
        /// Faz upload de uma imagem e valida o formato.
        /// </summary>
        Task<ImageUploadResultDto> UploadImageAsync(Stream imageStream, string fileName);

        /// <summary>
        /// Valida se o arquivo é uma imagem válida.
        /// </summary>
        bool ValidateImage(string fileName, long fileSize);
    }
}
