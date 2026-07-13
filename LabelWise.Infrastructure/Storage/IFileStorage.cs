using System.IO;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Storage
{
    public interface IFileStorage
    {
        Task<string> SaveTempAsync(Stream stream, string fileName);
        Task DeleteAsync(string path);
    }
}
