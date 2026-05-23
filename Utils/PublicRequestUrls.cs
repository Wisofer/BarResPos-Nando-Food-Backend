using Microsoft.Extensions.Configuration;

namespace BarRestPOS.Utils;

/// <summary>
/// URL pública del API (HTTPS y host correctos detrás de proxy). Usar en respuestas al cliente (p. ej. ticket cocina).
/// </summary>
public static class PublicRequestUrls
{
    public const string ConfigKeyPublicBaseUrl = "App:PublicBaseUrl";

    /// <summary>
    /// Base pública sin barra final. Si <see cref="ConfigKeyPublicBaseUrl"/> está vacío, usa el request (tras forwarded headers).
    /// </summary>
    public static string BaseUrl(HttpRequest request, IConfiguration configuration)
    {
        var configured = configuration[ConfigKeyPublicBaseUrl]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
    }

    public static string ImpresionCocinaAbsolute(HttpRequest request, IConfiguration configuration, int ordenId) =>
        $"{BaseUrl(request, configuration)}/api/v1/impresion/cocina/{ordenId}";
}
