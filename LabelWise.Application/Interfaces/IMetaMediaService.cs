using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces
{
    public interface IMetaMediaService
    {
        Task<string> DownloadMediaAsBase64Async(string mediaId);
        Task<byte[]> DownloadMediaAsBytesAsync(string mediaId); // ◄ Adicione esta linha

    }
}