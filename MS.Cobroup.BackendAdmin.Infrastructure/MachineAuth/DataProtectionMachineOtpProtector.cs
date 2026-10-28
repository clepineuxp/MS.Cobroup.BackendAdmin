using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using MS.Cobroup.BackendAdmin.Domain.Interfaces.Services;

namespace MS.Cobroup.BackendAdmin.Infrastructure.MachineAuth;

public sealed class DataProtectionMachineOtpProtector(IDataProtectionProvider provider) : IMachineOtpProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("CobroUp.MachineAuth.Otp.v1");

    public string Protect(string otp) => protector.Protect(otp);

    public string? Unprotect(string protectedOtp)
    {
        try { return protector.Unprotect(protectedOtp); }
        catch (CryptographicException) { return null; }
    }
}
