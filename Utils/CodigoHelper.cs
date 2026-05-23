namespace BarRestPOS.Utils;

public static class CodigoHelper
{
    private const string PrefijoCodigo = "EMS_";
    private const int LongitudNumeros = 6;
    private const int MinNumero = 100000;
    private const int MaxNumero = 999999;

    /// <summary>
    /// Genera un código aleatorio de cliente en formato EMS_XXXXXX (6 dígitos aleatorios)
    /// </summary>
    public static string GenerarCodigoCliente()
    {
        // Usar Random.Shared para thread-safety (disponible en .NET 6+)
        // Si no está disponible, usar ThreadLocal<Random> como alternativa
        var numeros = Random.Shared.Next(MinNumero, MaxNumero + 1); // Genera un número de 6 dígitos
        return $"{PrefijoCodigo}{numeros}";
    }

    /// <summary>
    /// Genera un código único de cliente, verificando que no exista en la base de datos
    /// </summary>
    public static string GenerarCodigoClienteUnico(Func<string, bool> existeCodigo)
    {
        string codigo;
        int intentos = 0;
        const int maxIntentos = 100; // Prevenir loops infinitos

        do
        {
            codigo = GenerarCodigoCliente();
            intentos++;
            
            if (intentos >= maxIntentos)
            {
                // Si después de muchos intentos no se encuentra uno único, usar timestamp
                var timestamp = DateTime.Now.Ticks % 1000000;
                codigo = $"{PrefijoCodigo}{timestamp:D6}";
                break;
            }
        } while (existeCodigo(codigo));

        return codigo;
    }
}

