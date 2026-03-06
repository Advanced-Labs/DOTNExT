using Spectre.Console;

namespace Scy.Commands;

public static class DevQuickstartCommand
{
    public static void Execute(string dir)
    {
        dir = Path.GetFullPath(dir);

        AnsiConsole.MarkupLine("[yellow]⚠ DEVELOPMENT MODE — keys are not production-safe[/]");
        AnsiConsole.WriteLine();

        // Init org
        InitOrgCommand.Execute("dev-org", dir);

        // Init a single node
        InitNodeCommand.Execute("dev-silo", dir);

        // Init a single user
        InitUserCommand.Execute("dev-user", dir);

        // Grant wildcard capability to user
        GrantCommand.Execute("dev-user", "scynapse.>", "*", dir);

        // Bundle both
        BundleCommand.ExecuteNode("dev-silo", dir);
        BundleCommand.ExecuteUser("dev-user", dir);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Dev quickstart complete![/]");
        AnsiConsole.MarkupLine("  Silo config:   {0}/dev-silo/silo-security.json", dir);
        AnsiConsole.MarkupLine("  Client config: {0}/dev-user/client-security.json", dir);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Usage in silo:[/]");
        AnsiConsole.MarkupLine("  builder.UseScynapseSecurity(config.GetSection(\"ScynapseSecurity\"));");
    }
}
