using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Verification;
using Spectre.Console;

namespace Scy.Commands;

public static class VerifyCommand
{
    public static void Execute(string filePath, string rootPubFile)
    {
        filePath = Path.GetFullPath(filePath);
        rootPubFile = Path.GetFullPath(rootPubFile);

        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] File not found: {0}", filePath);
            return;
        }
        if (!File.Exists(rootPubFile))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Root pub file not found: {0}", rootPubFile);
            return;
        }

        var assertion = SecurityConfigurationLoader.LoadAssertion(filePath);
        var rootPubKey = SecurityConfigurationLoader.LoadPublicKey(rootPubFile);

        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
        {
            rootPubKey
        };

        // Build a store with all assertions found alongside the target file
        var store = new InMemoryAssertionStore();
        var dir = Path.GetDirectoryName(filePath)!;
        var parentDir = Path.GetDirectoryName(dir);

        // Load assertions from the same directory and parent directory
        foreach (var searchDir in new[] { dir, parentDir })
        {
            if (searchDir is null || !Directory.Exists(searchDir)) continue;
            foreach (var file in Directory.GetFiles(searchDir, "*.assertion"))
            {
                try
                {
                    var a = SecurityConfigurationLoader.LoadAssertion(file);
                    store.StoreAsync(a).GetAwaiter().GetResult();
                }
                catch { /* skip unreadable files */ }
            }
        }

        // Also store the assertion being verified
        store.StoreAsync(assertion).GetAwaiter().GetResult();

        var verifier = new AssertionVerifier(store, new InMemoryNonceStore(), trustedRoots, new DefaultAttenuationChecker());
        var result = verifier.VerifyAsync(assertion).GetAwaiter().GetResult();

        if (result.IsValid)
        {
            AnsiConsole.MarkupLine("[green]VALID[/] — Chain verified successfully.");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]INVALID[/] — {0}", result.FailureReason ?? "Unknown failure");
        }
    }
}
