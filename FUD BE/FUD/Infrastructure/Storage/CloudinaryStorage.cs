using System.Text.RegularExpressions;
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
        public const long MaxImageBytes = 10 * 1024 * 1024;
        public const int MaxBatchCount = 5;

        private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "violations",
            "complaint-evidences",
            "meter-readings",
            "news",
            "requests"
        };

        private static readonly Regex SafeFolderRegex = new("^[a-z0-9-]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Lazy<Cloudinary> _cloudinary;

        public CloudinaryStorage(IOptions<CloudinarySettings> config, IConfiguration configuration)
        {
            var settings = config.Value;
            _cloudinary = new Lazy<Cloudinary>(() => CreateClient(settings, configuration));
        }

        private static Cloudinary CreateClient(CloudinarySettings settings, IConfiguration configuration)
        {
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
            var client = new Cloudinary(account);
            client.Api.Secure = true;
            return client;
        }

        private static string ValidateAndNormalizeFolder(string? folder)
        {
            var trimmed = (folder ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed)
                || !SafeFolderRegex.IsMatch(trimmed)
                || !AllowedFolders.Contains(trimmed))
            {
                throw new ArgumentException("Thư mục lưu trữ ảnh không hợp lệ.");
            }

            return trimmed.ToLowerInvariant();
        }

        private static string? DetectAllowedImageExtension(byte[] bytes, int length)
        {
            if (length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return ".jpg";
            }

            if (length >= 8
                && bytes[0] == 0x89
                && bytes[1] == 0x50
                && bytes[2] == 0x4E
                && bytes[3] == 0x47
                && bytes[4] == 0x0D
                && bytes[5] == 0x0A
                && bytes[6] == 0x1A
                && bytes[7] == 0x0A)
            {
                return ".png";
            }

            if (length >= 12
                && bytes[0] == (byte)'R'
                && bytes[1] == (byte)'I'
                && bytes[2] == (byte)'F'
                && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W'
                && bytes[9] == (byte)'E'
                && bytes[10] == (byte)'B'
                && bytes[11] == (byte)'P')
            {
                return ".webp";
            }

            return null;
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder = "violations")
        {
            var safeFolder = ValidateAndNormalizeFolder(folder);

            if (file == null || file.Length <= 0)
                throw new ArgumentException("Tệp ảnh tải lên không hợp lệ hoặc rỗng.");

            if (file.Length > MaxImageBytes)
                throw new ArgumentException("Ảnh tải lên không được vượt quá 10MB.");

            using var sourceStream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > MaxImageBytes)
                {
                    throw new ArgumentException("Ảnh tải lên không được vượt quá 10MB.");
                }
                memoryStream.Write(buffer, 0, bytesRead);
            }

            if (totalRead <= 0)
            {
                throw new ArgumentException("Tệp ảnh tải lên không hợp lệ hoặc rỗng.");
            }

            var header = memoryStream.GetBuffer();
            var ext = DetectAllowedImageExtension(header, (int)Math.Min(totalRead, 16));
            if (ext == null)
            {
                throw new ArgumentException("Chỉ chấp nhận tệp ảnh hợp lệ (JPG, PNG, WebP).");
            }

            memoryStream.Position = 0;
            var safeFileName = $"upload-{Guid.NewGuid():N}{ext}";

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(safeFileName, memoryStream),
                Folder = safeFolder,
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.Value.UploadAsync(uploadParams);
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
            ValidateAndNormalizeFolder(folder);
            if (files == null)
            {
                throw new ArgumentException("Danh sách tệp ảnh tải lên không hợp lệ.");
            }

            var list = files.ToList();
            if (list.Count > MaxBatchCount)
            {
                throw new ArgumentException("Tối đa 5 ảnh minh chứng cho mỗi lần tải lên.");
            }

            var urls = new List<string>();
            foreach (var file in list)
            {
                var url = await UploadImageAsync(file, folder);
                urls.Add(url);
            }
            return urls;
        }
    }
}
