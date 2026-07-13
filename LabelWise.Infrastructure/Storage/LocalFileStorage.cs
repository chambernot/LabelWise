using System;
using System.IO;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Storage
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _tempFolder;

        public LocalFileStorage()
        {
            try
            {
                _tempFolder = Path.Combine(Path.GetTempPath(), "labelwise");
                Console.WriteLine($"[LocalFileStorage] Creating temp folder: {_tempFolder}");

                if (!Directory.Exists(_tempFolder))
                {
                    Directory.CreateDirectory(_tempFolder);
                    Console.WriteLine($"[LocalFileStorage] Temp folder created successfully");
                }
                else
                {
                    Console.WriteLine($"[LocalFileStorage] Temp folder already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalFileStorage] ERROR creating temp folder: {ex.Message}");
                Console.WriteLine($"[LocalFileStorage] Stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to initialize LocalFileStorage: {ex.Message}", ex);
            }
        }

        public async Task<string> SaveTempAsync(Stream stream, string fileName)
        {
            var ext = Path.GetExtension(fileName) ?? string.Empty;
            var dst = Path.Combine(_tempFolder, $"{Guid.NewGuid()}{ext}");
            using var fs = File.Create(dst);
            await stream.CopyToAsync(fs);
            return dst;
        }

        public Task DeleteAsync(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // ignore
            }

            return Task.CompletedTask;
        }
    }
}
