using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Spectre.Console;

namespace Scy.Commands;

public static class GrantCommand
{
    public static void Execute(string to, string resource, string action, string orgDir)
    {
        orgDir = Path.GetFullPath(orgDir);
        var ctx = OrgContext.Load(orgDir);

        // Find target entity (user or node)
        var entity = ctx.FindUser(to) ?? ctx.FindNode(to);
        if (entity is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Entity '{0}' not found in org '{1}'.", to, ctx.Name);
            return;
        }

        // Load org key pair
        var orgSeedPath = Path.Combine(orgDir, ctx.OrgSeedFile);
        var orgSeedBytes = File.ReadAllBytes(orgSeedPath);
        var orgKp = ScynapseKeyPair.FromSeed(orgSeedBytes.AsSpan(1).ToArray(), (ScynapseKeyType)orgSeedBytes[0]);

        // Load org identity and entity delegation for proof chain
        var orgIdentityPath = Path.Combine(orgDir, ctx.OrgIdentityFile);
        var orgIdentity = SecurityConfigurationLoader.LoadAssertion(orgIdentityPath);

        // Load subject public key
        var subjectPubPath = Path.Combine(orgDir, entity.PubFile);
        var subjectPubKey = SecurityConfigurationLoader.LoadPublicKey(subjectPubPath);

        // Handle multiple actions (comma-separated)
        var actions = action.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var act in actions)
        {
            var safeName = $"{to}-{resource.Replace(".", "-").Replace(">", "gt").Replace("*", "star")}-{act}";
            var ccapFile = Path.Combine(orgDir, to, $"{safeName}.ccap");

            var ccap = AssertionBuilder.CreateCapability(
                orgKp, subjectPubKey,
                resource, act,
                proofs: new[] { orgIdentity.Id.ToArray() });

            SecurityConfigurationLoader.SaveAssertion(ccapFile, ccap);

            ctx.Grants.Add(new GrantEntry
            {
                To = to,
                Resource = resource,
                Action = act,
                CCapFile = $"{to}/{safeName}.ccap",
            });

            AnsiConsole.MarkupLine("[green]Granted[/] {0} → {1}:{2}", to, resource, act);
            AnsiConsole.MarkupLine("  {0}", ccapFile);
        }

        ctx.Save(orgDir);
    }
}
