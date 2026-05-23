using Amazon.S3;
using Amazon.S3.Model;
using BarRestPOS.Services.IServices;
using Microsoft.Extensions.Options;

namespace BarRestPOS.Services;

public class R2StorageOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public class R2StorageService : IR2StorageService
{
    private readonly R2StorageOptions _options;
    private readonly IAmazonS3 _s3Client;

    public R2StorageService(IOptions<R2StorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(_options.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(_options.BucketName) ||
            string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            throw new InvalidOperationException("R2 Storage no está configurado. Revise sección R2Storage en appsettings.");
        }

        var cfg = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true
        };
        _s3Client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, cfg);
    }

    public async Task<string> UploadProductImageAsync(int productoId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var key = $"productos/{productoId}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // R2 no implementa STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER en todos los flujos.
            DisablePayloadSigning = true
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{key}";
    }
}
