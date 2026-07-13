using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;

namespace LabelWise.Application.Interfaces
{
    public interface IProductAnalysisService
    {
        Task<ProductAnalysisResultDto> AnalyzeImageAsync(Stream stream, string fileName, System.Guid? userId = null);
    }
}
