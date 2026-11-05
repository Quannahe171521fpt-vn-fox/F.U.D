using Microsoft.AspNetCore.Http;

namespace FUD.Infrastructure.Storage
{
    public interface IFileStorage
    {
        Task<string> UploadImageAsync(IFormFile file, string folder = "violations");
        Task<List<string>> UploadImagesAsync(IEnumerable<IFormFile> files, string folder = "violations");
    }
}
