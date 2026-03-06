using System.CommandLine;
using Scy.Commands;

// Root command
var rootCommand = new RootCommand("Scynapse security provisioning CLI — topology-aware key and assertion management.");

// --- scy init org ---
var initCommand = new Command("init", "Initialize a new entity.");

var initOrgCommand = new Command("org", "Initialize a new organization (trust root).");
var orgNameOption = new Option<string>("--name", "Organization name") { IsRequired = true };
var orgDirOption = new Option<string>("--dir", () => ".", "Directory for the org context");
initOrgCommand.AddOption(orgNameOption);
initOrgCommand.AddOption(orgDirOption);
initOrgCommand.SetHandler(InitOrgCommand.Execute, orgNameOption, orgDirOption);

// --- scy init node ---
var initNodeCommand = new Command("node", "Initialize a new node in an organization.");
var nodeNameOption = new Option<string>("--name", "Node name") { IsRequired = true };
var nodeOrgOption = new Option<string>("--org", "Path to org directory") { IsRequired = true };
initNodeCommand.AddOption(nodeNameOption);
initNodeCommand.AddOption(nodeOrgOption);
initNodeCommand.SetHandler(InitNodeCommand.Execute, nodeNameOption, nodeOrgOption);

// --- scy init user ---
var initUserCommand = new Command("user", "Initialize a new user in an organization.");
var userNameOption = new Option<string>("--name", "User name") { IsRequired = true };
var userOrgOption = new Option<string>("--org", "Path to org directory") { IsRequired = true };
initUserCommand.AddOption(userNameOption);
initUserCommand.AddOption(userOrgOption);
initUserCommand.SetHandler(InitUserCommand.Execute, userNameOption, userOrgOption);

initCommand.AddCommand(initOrgCommand);
initCommand.AddCommand(initNodeCommand);
initCommand.AddCommand(initUserCommand);

// --- scy grant ---
var grantCommand = new Command("grant", "Grant a capability (CCap) to an entity.");
var grantToOption = new Option<string>("--to", "Target entity name (user or node)") { IsRequired = true };
var grantResourceOption = new Option<string>("--resource", "Resource pattern (e.g., scynapse.app.orders.>)") { IsRequired = true };
var grantActionOption = new Option<string>("--action", "Action(s), comma-separated (e.g., read,write)") { IsRequired = true };
var grantOrgOption = new Option<string>("--org", "Path to org directory") { IsRequired = true };
grantCommand.AddOption(grantToOption);
grantCommand.AddOption(grantResourceOption);
grantCommand.AddOption(grantActionOption);
grantCommand.AddOption(grantOrgOption);
grantCommand.SetHandler(GrantCommand.Execute, grantToOption, grantResourceOption, grantActionOption, grantOrgOption);

// --- scy bundle ---
var bundleCommand = new Command("bundle", "Generate complete deployment config for a node or user.");
var bundleNodeOption = new Option<string?>("--node", "Node name to bundle");
var bundleUserOption = new Option<string?>("--user", "User name to bundle");
var bundleOrgOption = new Option<string>("--org", "Path to org directory") { IsRequired = true };
bundleCommand.AddOption(bundleNodeOption);
bundleCommand.AddOption(bundleUserOption);
bundleCommand.AddOption(bundleOrgOption);
bundleCommand.SetHandler((string? node, string? user, string org) =>
{
    if (node is not null)
        BundleCommand.ExecuteNode(node, org);
    else if (user is not null)
        BundleCommand.ExecuteUser(user, org);
    else
        Console.Error.WriteLine("Error: specify --node or --user");
}, bundleNodeOption, bundleUserOption, bundleOrgOption);

// --- scy inspect ---
var inspectCommand = new Command("inspect", "Inspect a .seed, .pub, .assertion, or .ccap file.");
var inspectFileArg = new Argument<string>("file", "Path to the file to inspect");
inspectCommand.AddArgument(inspectFileArg);
inspectCommand.SetHandler(InspectCommand.Execute, inspectFileArg);

// --- scy verify ---
var verifyCommand = new Command("verify", "Verify an assertion's chain of trust.");
var verifyFileArg = new Argument<string>("file", "Path to the assertion/ccap file");
var verifyRootOption = new Option<string>("--root", "Path to trusted root .pub file") { IsRequired = true };
verifyCommand.AddArgument(verifyFileArg);
verifyCommand.AddOption(verifyRootOption);
verifyCommand.SetHandler(VerifyCommand.Execute, verifyFileArg, verifyRootOption);

// --- scy dev quickstart ---
var devCommand = new Command("dev", "Development helpers.");
var devQuickstartCommand = new Command("quickstart", "Generate everything for single-machine development.");
var devDirOption = new Option<string>("--dir", () => "./dev", "Output directory");
devQuickstartCommand.AddOption(devDirOption);
devQuickstartCommand.SetHandler(DevQuickstartCommand.Execute, devDirOption);
devCommand.AddCommand(devQuickstartCommand);

// Wire all commands
rootCommand.AddCommand(initCommand);
rootCommand.AddCommand(grantCommand);
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(inspectCommand);
rootCommand.AddCommand(verifyCommand);
rootCommand.AddCommand(devCommand);

return await rootCommand.InvokeAsync(args);
