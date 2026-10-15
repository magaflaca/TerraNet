using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TerraNet.Protocol;

public static class CertificatePin
{
    public static string Compute(X509Certificate certificate)
    {
        using var cert = new X509Certificate2(certificate);
        return Convert.ToBase64String(SHA256.HashData(cert.PublicKey.ExportSubjectPublicKeyInfo()));
    }

    public static bool Matches(X509Certificate? certificate, string pin) =>
        certificate is not null && CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(Compute(certificate)),
            Convert.FromBase64String(pin));
}
