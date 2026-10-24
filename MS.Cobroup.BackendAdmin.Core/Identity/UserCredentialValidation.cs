namespace MS.Cobroup.BackendAdmin.Core.Identity;

public static class UserCredentialValidation
{
    public static void Validate(string username, string displayName, string password, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 80)
            throw new ArgumentException("El usuario es obligatorio y admite hasta 80 caracteres.");
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 160)
            throw new ArgumentException("El nombre es obligatorio y admite hasta 160 caracteres.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
            throw new ArgumentException("La contraseña debe tener entre 12 y 128 caracteres.");
        var phone = phoneNumber?.Trim() ?? string.Empty;
        if (phone.Length is < 7 or > 24 || phone.Count(char.IsAsciiDigit) < 7
            || phone.Any(character => !char.IsAsciiDigit(character) && character is not ('+' or ' ' or '-' or '(' or ')')))
            throw new ArgumentException("Ingresa un teléfono válido de 7 a 24 caracteres.");
    }
}
