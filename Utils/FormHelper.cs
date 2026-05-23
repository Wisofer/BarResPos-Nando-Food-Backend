using Microsoft.AspNetCore.Http;

namespace BarRestPOS.Utils;

public static class FormHelper
{
    /// <summary>
    /// Obtiene el valor de un checkbox del formulario
    /// </summary>
    public static bool GetCheckboxValue(IFormCollection form, string name)
    {
        if (!form.ContainsKey(name))
            return false;

        var values = form[name];
        return values.Count > 0 && values.Any(v => v == "true");
    }
}

