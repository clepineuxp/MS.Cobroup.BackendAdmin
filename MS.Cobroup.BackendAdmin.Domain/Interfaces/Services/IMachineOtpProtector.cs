namespace MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

public interface IMachineOtpProtector
{
    string Protect(string otp);
    string? Unprotect(string protectedOtp);
}
