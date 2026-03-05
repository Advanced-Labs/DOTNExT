using PeterO.Cbor;

namespace Scynapse.Security.Assertions;

/// <summary>
/// CBOR-based serialization for SignedAssertion.
/// Deterministic: uses canonical CBOR (CTAP2) with sorted map keys.
/// Wire format v1.
/// </summary>
internal static class AssertionSerializer
{
    // CBOR map keys (integers for compactness, sorted for determinism)
    private const int KeyVersion = 1;
    private const int KeyIssuer = 2;
    private const int KeySubject = 3;
    private const int KeyClaimType = 4;
    private const int KeyClaimData = 5;
    private const int KeyNotBefore = 6;
    private const int KeyExpiresAt = 7;
    private const int KeyNonce = 8;
    private const int KeyProofs = 9;
    private const int KeyExtensions = 10;
    private const int KeyId = 11;
    private const int KeySignature = 12;

    /// <summary>
    /// Serialize content fields to CBOR bytes (everything except Id and Signature).
    /// Used for computing the content hash (Id).
    /// Deterministic: CTAP2 canonical form, integer keys in sorted order.
    /// </summary>
    internal static byte[] SerializeContentFields(SignedAssertion a)
    {
        var map = CBORObject.NewOrderedMap();
        map.Add(KeyVersion, a.Version);
        map.Add(KeyIssuer, a.Issuer.ToArray());
        map.Add(KeySubject, a.Subject.ToArray());
        map.Add(KeyClaimType, (int)a.ClaimType);
        map.Add(KeyClaimData, a.ClaimData.ToArray());

        if (a.NotBefore.HasValue) map.Add(KeyNotBefore, a.NotBefore.Value);
        if (a.ExpiresAt.HasValue) map.Add(KeyExpiresAt, a.ExpiresAt.Value);
        if (a.Nonce.HasValue) map.Add(KeyNonce, a.Nonce.Value.ToArray());

        var proofsArray = CBORObject.NewArray();
        foreach (var proof in a.Proofs)
            proofsArray.Add(proof.ToArray());
        map.Add(KeyProofs, proofsArray);

        var extMap = CBORObject.NewOrderedMap();
        foreach (var key in a.Extensions.Keys.OrderBy(k => k, StringComparer.Ordinal))
            extMap.Add(key, a.Extensions[key].ToArray());
        map.Add(KeyExtensions, extMap);

        return map.EncodeToBytes(CBOREncodeOptions.DefaultCtap2Canonical);
    }

    /// <summary>
    /// Build the bytes that get signed: content fields CBOR || Id.
    /// </summary>
    internal static byte[] BuildSignableBytes(byte[] contentBytes, ReadOnlyMemory<byte> id)
    {
        var result = new byte[contentBytes.Length + 32];
        contentBytes.CopyTo(result, 0);
        id.Span.CopyTo(result.AsSpan(contentBytes.Length));
        return result;
    }

    /// <summary>
    /// Full wire format: CBOR map with all fields including Id and Signature.
    /// </summary>
    internal static byte[] Serialize(SignedAssertion a)
    {
        var map = CBORObject.NewOrderedMap();
        map.Add(KeyVersion, a.Version);
        map.Add(KeyIssuer, a.Issuer.ToArray());
        map.Add(KeySubject, a.Subject.ToArray());
        map.Add(KeyClaimType, (int)a.ClaimType);
        map.Add(KeyClaimData, a.ClaimData.ToArray());

        if (a.NotBefore.HasValue) map.Add(KeyNotBefore, a.NotBefore.Value);
        if (a.ExpiresAt.HasValue) map.Add(KeyExpiresAt, a.ExpiresAt.Value);
        if (a.Nonce.HasValue) map.Add(KeyNonce, a.Nonce.Value.ToArray());

        var proofsArray = CBORObject.NewArray();
        foreach (var proof in a.Proofs)
            proofsArray.Add(proof.ToArray());
        map.Add(KeyProofs, proofsArray);

        var extMap = CBORObject.NewOrderedMap();
        foreach (var key in a.Extensions.Keys.OrderBy(k => k, StringComparer.Ordinal))
            extMap.Add(key, a.Extensions[key].ToArray());
        map.Add(KeyExtensions, extMap);

        map.Add(KeyId, a.Id.ToArray());
        map.Add(KeySignature, a.Signature.ToArray());

        return map.EncodeToBytes(CBOREncodeOptions.DefaultCtap2Canonical);
    }

    /// <summary>
    /// Deserialize from CBOR wire format.
    /// </summary>
    internal static SignedAssertion Deserialize(ReadOnlySpan<byte> data)
    {
        var map = CBORObject.DecodeFromBytes(data.ToArray());

        byte version = (byte)map[KeyVersion].AsInt32();
        byte[] issuer = map[KeyIssuer].GetByteString();
        byte[] subject = map[KeySubject].GetByteString();
        var claimType = (ClaimType)map[KeyClaimType].AsInt32();
        byte[] claimData = map[KeyClaimData].GetByteString();

        long? notBefore = map.ContainsKey(KeyNotBefore) ? map[KeyNotBefore].ToObject<long>() : null;
        long? expiresAt = map.ContainsKey(KeyExpiresAt) ? map[KeyExpiresAt].ToObject<long>() : null;
        ReadOnlyMemory<byte>? nonce = map.ContainsKey(KeyNonce)
            ? new ReadOnlyMemory<byte>(map[KeyNonce].GetByteString())
            : null;

        var proofsObj = map[KeyProofs];
        var proofs = new ReadOnlyMemory<byte>[proofsObj.Count];
        for (int i = 0; i < proofsObj.Count; i++)
            proofs[i] = proofsObj[i].GetByteString();

        var extObj = map[KeyExtensions];
        var extensions = new Dictionary<string, ReadOnlyMemory<byte>>();
        foreach (var key in extObj.Keys)
            extensions[key.AsString()] = extObj[key].GetByteString();

        byte[] id = map[KeyId].GetByteString();
        byte[] signature = map[KeySignature].GetByteString();

        return new SignedAssertion(
            version, id, issuer, subject, claimType, claimData,
            notBefore, expiresAt, nonce, proofs, extensions, signature);
    }
}
