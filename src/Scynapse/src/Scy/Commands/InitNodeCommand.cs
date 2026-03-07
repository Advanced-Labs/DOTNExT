using System.Text.Json;
using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Spectre.Console;

namespace Scy.Commands;

public static class InitNodeCommand
{
    public static void Execute(string name, string orgDir)
    {
        orgDir = Path.GetFullPath(orgDir);
        var ctx = OrgContext.Load(orgDir);

        if (ctx.FindNode(name) is not null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Node '{0}' already exists in org '{1}'.", name, ctx.Name);
            return;
        }

        // Load org key pair
        var orgSeedPath = Path.Combine(orgDir, ctx.OrgSeedFile);
        var orgSeedBytes = File.ReadAllBytes(orgSeedPath);
        var orgKp = ScynapseKeyPair.FromSeed(orgSeedBytes.AsSpan(1).ToArray(), (ScynapseKeyType)orgSeedBytes[0]);

        // Load org identity for proof chain
        var orgIdentityPath = Path.Combine(orgDir, ctx.OrgIdentityFile);
        var orgIdentity = SecurityConfigurationLoader.LoadAssertion(orgIdentityPath);

        // Generate node key
        var nodeDir = Path.Combine(orgDir, name);
        Directory.CreateDirectory(nodeDir);

        var nodeKp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var seedFile = Path.Combine(nodeDir, "node.seed");
        var pubFile = Path.Combine(nodeDir, "node.pub");
        var delegationFile = Path.Combine(nodeDir, "node-delegation.assertion");

        SecurityConfigurationLoader.SaveSeed(seedFile, nodeKp, ScynapseKeyType.Node);
        SecurityConfigurationLoader.SavePublicKey(pubFile, nodeKp.PublicKeyBytes.ToArray(), ScynapseKeyType.Node);

        // Create delegation: org → node
        var delegation = AssertionBuilder.CreateDelegation(
            orgKp, nodeKp.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { orgIdentity.Id.ToArray() },
            resourcePattern: "scynapse.>",
            actionPattern: "*");
        SecurityConfigurationLoader.SaveAssertion(delegationFile, delegation);

        // Generate silo-security.json config
        var configFile = Path.Combine(nodeDir, "silo-security.json");
        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = "./node.seed",
            TrustedRoots = new List<string> { "../org.pub" },
            BootstrapAssertionFiles = new List<string>
            {
                "../org-identity.assertion",
                "./node-delegation.assertion",
            },
            PeerAssertionDirectory = "./peers/",
            BootstrapCapabilityFiles = new List<string>(),
            EnableTls = true,
            RequireMutualTls = true,
        };
        Directory.CreateDirectory(Path.Combine(nodeDir, "peers"));
        File.WriteAllText(configFile, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        // Update org context
        ctx.Nodes.Add(new EntityEntry
        {
            Name = name,
            SeedFile = $"{name}/node.seed",
            PubFile = $"{name}/node.pub",
            DelegationFile = $"{name}/node-delegation.assertion",
            ConfigFile = $"{name}/silo-security.json",
        });
        ctx.Save(orgDir);

        AnsiConsole.MarkupLine("[green]Created node[/] '{0}' in org '{1}'", name, ctx.Name);
        AnsiConsole.MarkupLine("  {0}/node.seed                (keep secret!)", name);
        AnsiConsole.MarkupLine("  {0}/node.pub", name);
        AnsiConsole.MarkupLine("  {0}/node-delegation.assertion", name);
        AnsiConsole.MarkupLine("  {0}/silo-security.json       (ready for UseScynapseSecurity)", name);
    }
}
