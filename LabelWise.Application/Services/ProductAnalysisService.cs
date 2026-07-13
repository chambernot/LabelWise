using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Services
{
    // Implementation is placed in Infrastructure project to avoid circular references.
    // This placeholder keeps the interface available for compilation in this project.
    public class ProductAnalysisService : IProductAnalysisService
    {
        public Task<ProductAnalysisResultDto> AnalyzeImageAsync(Stream stream, string fileName, Guid? userId = null)
        {
            throw new NotImplementedException("ProductAnalysisService is implemented in the Infrastructure project.");
        }
    }
}
