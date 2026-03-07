using System.Text.Json;
using Scynapse.Security.Configuration;
using Spectre.Console;

namespace Scy.Commands;

public static class BundleCommand
{
    public static void ExecuteNode(string nodeName, string orgDir)
    {
        orgDir = Path.GetFullPath(orgDir);
        var ctx = OrgContext.Load(orgDir);

        var node = ctx.FindNode(nodeName);
        if (node is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Node '{0}' not found in org '{1}'.", nodeName, ctx.Name);
            return;
        }

        var nodeDir = Path.Combine(orgDir, nodeName);
        var peersDir = Path.Combine(nodeDir, "peers");
        Directory.CreateDirectory(peersDir);

        // Copy all other nodes' delegation assertions into this node's peers directory
        var peerCount = 0;
        foreach (var otherNode in ctx.Nodes)
        {
            if (otherNode.Name == nodeName) continue;
            var srcDelegation = Path.Combine(orgDir, otherNode.DelegationFile);
            if (!File.Exists(srcDelegation)) continue;
            var dstDelegation = Path.Combine(peersDir, $"{otherNode.Name}-delegation.assertion");
            File.Copy(srcDelegation, dstDelegation, overwrite: true);
            peerCount++;
        }

        // Regenerate silo-security.json with all bootstrap assertions and capabilities
        var bootstrapAssertions = new List<string>
        {
            "../org-identity.assertion",
            "./node-delegation.assertion",
        };

        // Include user delegation assertions (so the silo can verify user CCaps)
        foreach (var user in ctx.Users)
        {
            var srcDelegation = Path.Combine(orgDir, user.DelegationFile);
            if (!File.Exists(srcDelegation)) continue;
            var dstDelegation = Path.Combine(nodeDir, $"{user.Name}-delegation.assertion");
            File.Copy(srcDelegation, dstDelegation, overwrite: true);
            bootstrapAssertions.Add($"./{user.Name}-delegation.assertion");
        }

        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = "./node.seed",
            TrustedRoots = new List<string> { "../org.pub" },
            BootstrapAssertionFiles = bootstrapAssertions,
            PeerAssertionDirectory = "./peers/",
            BootstrapCapabilityFiles = new List<string>(),
            EnableTls = true,
            RequireMutualTls = true,
        };

        var configPath = Path.Combine(nodeDir, "silo-security.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        AnsiConsole.MarkupLine("[green]Bundled node[/] '{0}': {1} peers, {2} user delegations", nodeName, peerCount, ctx.Users.Count);
        AnsiConsole.MarkupLine("  {0}/silo-security.json (updated)", nodeName);
    }

    public static void ExecuteUser(string userName, string orgDir)
    {
        orgDir = Path.GetFullPath(orgDir);
        var ctx = OrgContext.Load(orgDir);

        var user = ctx.FindUser(userName);
        if (user is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] User '{0}' not found in org '{1}'.", userName, ctx.Name);
            return;
        }

        var userDir = Path.Combine(orgDir, userName);

        // Collect all CCaps granted to this user
        var ccapFiles = new List<string>();
        foreach (var grant in ctx.Grants.Where(g => g.To == userName))
        {
            var srcCCap = Path.Combine(orgDir, grant.CCapFile);
            if (!File.Exists(srcCCap)) continue;
            var dstCCap = Path.Combine(userDir, Path.GetFileName(grant.CCapFile));
            if (srcCCap != dstCCap)
                File.Copy(srcCCap, dstCCap, overwrite: true);
            ccapFiles.Add($"./{Path.GetFileName(grant.CCapFile)}");
        }

        // Generate client-security.json
        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = "./user.seed",
            TrustedRoots = new List<string> { "../org.pub" },
            BootstrapAssertionFiles = new List<string>
            {
                "../org-identity.assertion",
                "./user-delegation.assertion",
            },
            BootstrapCapabilityFiles = ccapFiles,
            EnableTls = true,
            RequireMutualTls = false,
        };

        var configPath = Path.Combine(userDir, "client-security.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        AnsiConsole.MarkupLine("[green]Bundled user[/] '{0}': {1} capabilities", userName, ccapFiles.Count);
        AnsiConsole.MarkupLine("  {0}/client-security.json (ready for UseScynapseSecurity)", userName);
    }
}
