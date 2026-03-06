using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Spectre.Console;

namespace Scy.Commands;

public static class InitOrgCommand
{
    public static void Execute(string name, string dir)
    {
        dir = Path.GetFullPath(dir);
        if (File.Exists(OrgContext.GetContextPath(dir)))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Org already exists at {0}", dir);
            return;
        }

        Directory.CreateDirectory(dir);

        var orgKp = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var seedPath = Path.Combine(dir, "org.seed");
        var pubPath = Path.Combine(dir, "org.pub");
        var identityPath = Path.Combine(dir, "org-identity.assertion");

        SecurityConfigurationLoader.SaveSeed(seedPath, orgKp, ScynapseKeyType.Organization);
        SecurityConfigurationLoader.SavePublicKey(pubPath, orgKp.PublicKeyBytes, ScynapseKeyType.Organization);

        var identity = AssertionBuilder.CreateIdentity(orgKp);
        SecurityConfigurationLoader.SaveAssertion(identityPath, identity);

        var ctx = new OrgContext
        {
            Name = name,
            OrgSeedFile = "org.seed",
            OrgPubFile = "org.pub",
            OrgIdentityFile = "org-identity.assertion",
        };
        ctx.Save(dir);

        AnsiConsole.MarkupLine("[green]Created org[/] '{0}' at {1}", name, dir);
        AnsiConsole.MarkupLine("  org.seed            (keep secret!)");
        AnsiConsole.MarkupLine("  org.pub             (distribute to nodes)");
        AnsiConsole.MarkupLine("  org-identity.assertion");
    }
}
