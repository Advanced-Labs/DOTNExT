using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Spectre.Console;

namespace Scy.Commands;

public static class InitUserCommand
{
    public static void Execute(string name, string orgDir)
    {
        orgDir = Path.GetFullPath(orgDir);
        var ctx = OrgContext.Load(orgDir);

        if (ctx.FindUser(name) is not null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] User '{0}' already exists in org '{1}'.", name, ctx.Name);
            return;
        }

        // Load org key pair
        var orgSeedPath = Path.Combine(orgDir, ctx.OrgSeedFile);
        var orgSeedBytes = File.ReadAllBytes(orgSeedPath);
        var orgKp = ScynapseKeyPair.FromSeed(orgSeedBytes.AsSpan(1).ToArray(), (ScynapseKeyType)orgSeedBytes[0]);

        // Load org identity for proof chain
        var orgIdentityPath = Path.Combine(orgDir, ctx.OrgIdentityFile);
        var orgIdentity = SecurityConfigurationLoader.LoadAssertion(orgIdentityPath);

        // Generate user key
        var userDir = Path.Combine(orgDir, name);
        Directory.CreateDirectory(userDir);

        var userKp = ScynapseKeyPair.Generate(ScynapseKeyType.User);
        var seedFile = Path.Combine(userDir, "user.seed");
        var pubFile = Path.Combine(userDir, "user.pub");
        var delegationFile = Path.Combine(userDir, "user-delegation.assertion");

        SecurityConfigurationLoader.SaveSeed(seedFile, userKp, ScynapseKeyType.User);
        SecurityConfigurationLoader.SavePublicKey(pubFile, userKp.PublicKeyBytes.ToArray(), ScynapseKeyType.User);

        // Create delegation: org → user (limited to capability claims)
        var delegation = AssertionBuilder.CreateDelegation(
            orgKp, userKp.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { orgIdentity.Id.ToArray() });
        SecurityConfigurationLoader.SaveAssertion(delegationFile, delegation);

        ctx.Users.Add(new EntityEntry
        {
            Name = name,
            SeedFile = $"{name}/user.seed",
            PubFile = $"{name}/user.pub",
            DelegationFile = $"{name}/user-delegation.assertion",
        });
        ctx.Save(orgDir);

        AnsiConsole.MarkupLine("[green]Created user[/] '{0}' in org '{1}'", name, ctx.Name);
        AnsiConsole.MarkupLine("  {0}/user.seed                (keep secret!)", name);
        AnsiConsole.MarkupLine("  {0}/user.pub", name);
        AnsiConsole.MarkupLine("  {0}/user-delegation.assertion", name);
    }
}
