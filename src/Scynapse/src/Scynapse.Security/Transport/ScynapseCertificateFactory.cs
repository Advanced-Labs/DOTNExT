using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Scynapse.Security.Crypto;

namespace Scynapse.Security.Transport;

/// <summary>
/// Creates self-signed X.509 certificates for Scynapse TLS transport.
///
/// Workaround for SslStream lacking Ed25519 support. The certificate uses an ephemeral
/// ECDSA P-256 key for the TLS handshake (transport confidentiality only) and embeds
/// the node's Ed25519 public key in a custom X.509 extension. The ECDSA key has no
/// security meaning in Scynapse's model — the real identity is the Ed25519 key.
///
/// Replace with direct Ed25519 cert when .NET supports Ed25519 in TLS handshakes.
/// When that happens, only this class and <see cref="ScynapseRemoteCertificateValidator"/>
/// need to change — the rest of the security stack is Ed25519-native.
/// </summary>
public static class ScynapseCertificateFactory
{
    /// <summary>
    /// Private-use OID for the Scynapse Ed25519 identity extension.
    /// Value: ScynapseKeyType (1 byte) || Ed25519 public key (32 bytes).
    /// </summary>
    public const string Ed25519ExtensionOid = "1.3.6.1.4.1.99999.1.1";

    /// <summary>
    /// Creates a self-signed X.509 certificate for TLS transport.
    /// The ECDSA key handles TLS handshakes; the Ed25519 identity is in a custom extension.
    /// </summary>
    public static X509Certificate2 CreateSelfSigned(ScynapseKeyPair nodeKeyPair)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var subject = new X500DistinguishedName("CN=Scynapse Node, O=Scynapse");
        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);

        // Embed Ed25519 public key + key type in a critical custom extension.
        // Critical because any peer MUST understand this to authenticate.
        var extensionData = new byte[33];
        extensionData[0] = (byte)nodeKeyPair.KeyType;
        nodeKeyPair.PublicKeyBytes.CopyTo(extensionData.AsSpan(1));
        request.CertificateExtensions.Add(
            new X509Extension(new Oid(Ed25519ExtensionOid, "Scynapse Ed25519 Identity"), extensionData, critical: true));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddYears(1);

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>
    /// Extracts the Ed25519 identity data from a certificate's custom extension.
    /// Returns the raw extension value (1 byte key type + 32 bytes public key), or null
    /// if the certificate doesn't contain the Scynapse Ed25519 extension.
    /// </summary>
    public static byte[]? ExtractEd25519PublicKey(X509Certificate2 certificate)
    {
        var ext = certificate.Extensions[Ed25519ExtensionOid];
        if (ext is null)
            return null;

        var raw = ext.RawData;
        if (raw.Length != 33) // 1 byte type + 32 bytes Ed25519 key
            return null;

        return raw;
    }
}
