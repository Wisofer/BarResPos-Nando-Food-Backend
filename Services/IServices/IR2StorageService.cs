namespace BarRestPOS.Services.IServices;

public interface IR2StorageService
{
    Task<string> UploadProductImageAsync(int productoId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
}
