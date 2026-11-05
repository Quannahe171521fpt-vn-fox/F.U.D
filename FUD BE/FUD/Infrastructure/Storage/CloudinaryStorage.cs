using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FUD.Infrastructure.Storage
{
    public class CloudinaryStorage : IFileStorage
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryStorage(IOptions<CloudinarySettings> config, IConfiguration configuration)
        {
            var settings = config.Value;

            // Prioritize configuration from options, fallback to direct configuration keys
            var cloudName = !string.IsNullOrWhiteSpace(settings.CloudName) && settings.CloudName != "xxxx"
                ? settings.CloudName
                : configuration["Cloudinary:CloudName"] ?? configuration["Cloudinary__CloudName"];

            var apiKey = !string.IsNullOrWhiteSpace(settings.ApiKey) && settings.ApiKey != "xxxx"
                ? settings.ApiKey
                : configuration["Cloudinary:ApiKey"] ?? configuration["Cloudinary__ApiKey"];

            var apiSecret = !string.IsNullOrWhiteSpace(settings.ApiSecret) && settings.ApiSecret != "xxxx"
                ? settings.ApiSecret
                : configuration["Cloudinary:ApiSecret"] ?? configuration["Cloudinary__ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                throw new InvalidOperationException("Chưa cấu hình đầy đủ thông tin Cloudinary (CloudName, ApiKey, ApiSecret). Vui lòng kiểm tra file cấu hình hoặc .env!");
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder = "violations")
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Tệp tải lên không hợp lệ hoặc rỗng");

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException($"Lỗi tải ảnh lên Cloudinary: {uploadResult.Error.Message}");
            }

            var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("Không nhận được URL ảnh từ Cloudinary.");
            }

            return url;
        }

        public async Task<List<string>> UploadImagesAsync(IEnumerable<IFormFile> files, string folder = "violations")
        {
            var urls = new List<string>();
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var url = await UploadImageAsync(file, folder);
                    urls.Add(url);
                }
            }
            return urls;
        }
    }
}
