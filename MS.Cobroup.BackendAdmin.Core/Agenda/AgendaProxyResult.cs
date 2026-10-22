namespace MS.Cobroup.BackendAdmin.Core.Agenda;

public sealed record AgendaProxyResult(int StatusCode, string? Content, string ContentType)
{
    public static AgendaProxyResult Unavailable() =>
        new(503, "{\"title\":\"Servicio operativo no disponible\",\"status\":503}", "application/problem+json");

    public static AgendaProxyResult Forbidden(string message = "Acceso denegado a los recursos solicitados.") =>
        new(403, $"{{\"title\":\"Acceso denegado\",\"detail\":\"{message}\",\"status\":403}}", "application/problem+json");
}
