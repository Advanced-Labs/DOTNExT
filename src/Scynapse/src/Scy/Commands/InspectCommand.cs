using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Spectre.Console;

namespace Scy.Commands;

public static class InspectCommand
{
    public static void Execute(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] File not found: {0}", filePath);
            return;
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        switch (ext)
        {
            case ".assertion":
            case ".ccap":
                InspectAssertion(filePath);
                break;
            case ".pub":
                InspectPublicKey(filePath);
                break;
            case ".seed":
                InspectSeed(filePath);
                break;
            default:
                AnsiConsole.MarkupLine("[yellow]Unknown file type:[/] {0}", ext);
                break;
        }
    }

    private static void InspectAssertion(string path)
    {
        var assertion = SecurityConfigurationLoader.LoadAssertion(path);

        var table = new Table();
        table.AddColumn("Field");
        table.AddColumn("Value");

        table.AddRow("File", Path.GetFileName(path));
        table.AddRow("Version", assertion.Version.ToString());
        table.AddRow("ID", Convert.ToHexString(assertion.Id.Span));
        table.AddRow("Claim Type", assertion.ClaimType.ToString());
        table.AddRow("Issuer", TruncateHex(assertion.Issuer.Span));
        table.AddRow("Subject", TruncateHex(assertion.Subject.Span));

        if (assertion.NotBefore.HasValue)
            table.AddRow("Not Before", DateTimeOffset.FromUnixTimeSeconds(assertion.NotBefore.Value).ToString("u"));
        if (assertion.ExpiresAt.HasValue)
            table.AddRow("Expires At", DateTimeOffset.FromUnixTimeSeconds(assertion.ExpiresAt.Value).ToString("u"));

        table.AddRow("Proofs", assertion.Proofs.Count.ToString());
        for (int i = 0; i < assertion.Proofs.Count; i++)
            table.AddRow($"  Proof[{i}]", TruncateHex(assertion.Proofs[i].Span));

        // Decode claim data based on type
        switch (assertion.ClaimType)
        {
            case ClaimType.Capability:
                var cap = CapabilityClaim.Deserialize(assertion.ClaimData.Span);
                table.AddRow("Resource", cap.Resource);
                table.AddRow("Action", cap.Action);
                break;
            case ClaimType.Delegation:
                var del = DelegationClaim.Deserialize(assertion.ClaimData.Span);
                table.AddRow("Allowed Claims", string.Join(", ", del.AllowedClaimTypes));
                if (del.ResourcePattern is not null)
                    table.AddRow("Resource Pattern", del.ResourcePattern);
                if (del.ActionPattern is not null)
                    table.AddRow("Action Pattern", del.ActionPattern);
                if (del.MaxDepth.HasValue)
                    table.AddRow("Max Depth", del.MaxDepth.Value.ToString());
                break;
            case ClaimType.Identity:
                table.AddRow("(Self-signed identity)", "");
                break;
            case ClaimType.Revocation:
                var rev = RevocationClaim.Deserialize(assertion.ClaimData.Span);
                table.AddRow("Revoked ID", Convert.ToHexString(rev.Target));
                if (rev.Reason is not null)
                    table.AddRow("Reason", rev.Reason);
                break;
        }

        table.AddRow("Signature", TruncateHex(assertion.Signature.Span));

        AnsiConsole.Write(table);
    }

    private static void InspectPublicKey(string path)
    {
        var text = File.ReadAllText(path).Trim();
        var (keyType, pubKey) = ScynapseKeyEncoding.DecodePublicKey(text);

        var table = new Table();
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("File", Path.GetFileName(path));
        table.AddRow("Key Type", keyType.ToString());
        table.AddRow("Public Key", Convert.ToHexString(pubKey));
        table.AddRow("Encoded", text);

        AnsiConsole.Write(table);
    }

    private static void InspectSeed(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var keyType = (ScynapseKeyType)bytes[0];

        var table = new Table();
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("File", Path.GetFileName(path));
        table.AddRow("Key Type", keyType.ToString());
        table.AddRow("Seed Length", $"{bytes.Length - 1} bytes");
        table.AddRow("[red]WARNING[/]", "Seed file contains private key material!");

        AnsiConsole.Write(table);
    }

    private static string TruncateHex(ReadOnlySpan<byte> bytes)
    {
        var hex = Convert.ToHexString(bytes);
        return hex.Length > 16 ? hex[..16] + "..." : hex;
    }
}
