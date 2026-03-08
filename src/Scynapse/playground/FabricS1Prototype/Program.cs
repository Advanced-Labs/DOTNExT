using System.Text.Json;

namespace FabricS1Prototype;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static int Main(string[] args)
    {
        var fixtureDirectory = ResolveFixtureDirectory(args);
        if (fixtureDirectory is null)
        {
            Console.Error.WriteLine("Could not locate fixture directory. Provide it as the first argument.");
            return 2;
        }

        var fixtureFiles = Directory.GetFiles(fixtureDirectory, "TV-*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (fixtureFiles.Length == 0)
        {
            Console.Error.WriteLine($"No fixture files found in: {fixtureDirectory}");
            return 2;
        }

        var engine = new ConformanceEngine();
        var results = new List<VectorResult>(fixtureFiles.Length);

        foreach (var fixtureFile in fixtureFiles)
        {
            var fixture = LoadFixture(fixtureFile);
            if (fixture is null)
            {
                Console.WriteLine($"FAIL {Path.GetFileNameWithoutExtension(fixtureFile)}: invalid JSON fixture.");
                results.Add(new VectorResult
                {
                    Id = Path.GetFileNameWithoutExtension(fixtureFile),
                    Passed = false,
                    Errors = new List<string> { "Failed to deserialize fixture JSON." },
                    ObservedStateTrace = Array.Empty<string>(),
                    ObservedDenyCode = null,
                    ObservedRetryable = null
                });
                continue;
            }

            var result = engine.Evaluate(fixture);

            var expectsFailure = string.Equals(fixture.ExpectConformance, "fail", StringComparison.OrdinalIgnoreCase);
            if (expectsFailure)
            {
                result.EffectivePassed = !result.Passed;
                if (result.Passed)
                {
                    result.Errors.Add("[EXPECT] Expected conformance failure but vector passed.");
                }

                if (fixture.ExpectedErrorContains.Count > 0)
                {
                    foreach (var expectedToken in fixture.ExpectedErrorContains)
                    {
                        var found = result.Errors.Any(error =>
                            error.IndexOf(expectedToken, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!found)
                        {
                            result.Errors.Add($"[EXPECT] Missing expected error token: '{expectedToken}'.");
                        }
                    }
                }

                if (result.Errors.Any(error => error.StartsWith("[EXPECT]", StringComparison.Ordinal)))
                {
                    result.EffectivePassed = false;
                }
            }
            else
            {
                result.EffectivePassed = result.Passed;
            }

            results.Add(result);

            var status = result.EffectivePassed ? "PASS" : "FAIL";
            var expectationNote = expectsFailure ? " [expected fail]" : string.Empty;
            Console.WriteLine($"{status} {result.Id} - {fixture.Title}{expectationNote}");

            if (!result.EffectivePassed || expectsFailure)
            {
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        }

        PrintSummary(results);
        return results.All(r => r.EffectivePassed) ? 0 : 1;
    }

    private static FixtureCase? LoadFixture(string fixturePath)
    {
        var json = File.ReadAllText(fixturePath);
        return JsonSerializer.Deserialize<FixtureCase>(json, JsonOptions);
    }

    private static string? ResolveFixtureDirectory(string[] args)
    {
        if (args.Length > 0 && Directory.Exists(args[0]))
        {
            return Path.GetFullPath(args[0]);
        }

        var cwd = Directory.GetCurrentDirectory();
        var probe = new DirectoryInfo(cwd);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, "Docs", "Scynapse", "Design", "Fixtures", "S1");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        return null;
    }

    private static void PrintSummary(IReadOnlyList<VectorResult> results)
    {
        var passed = results.Count(r => r.EffectivePassed);
        var failed = results.Count - passed;

        Console.WriteLine();
        Console.WriteLine("=== S1 Conformance Summary ===");
        Console.WriteLine($"Vectors: {results.Count}");
        Console.WriteLine($"Pass:    {passed}");
        Console.WriteLine($"Fail:    {failed}");

        var layerFailures = results
            .SelectMany(r => r.Errors)
            .Select(error => ExtractLayer(error))
            .Where(layer => layer is not null)
            .GroupBy(layer => layer!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}")
            .ToArray();

        if (layerFailures.Length == 0)
        {
            Console.WriteLine("Layer failures: none");
        }
        else
        {
            Console.WriteLine($"Layer failures: {string.Join(", ", layerFailures)}");
        }
    }

    private static string? ExtractLayer(string error)
    {
        if (!error.StartsWith("[", StringComparison.Ordinal))
        {
            return null;
        }

        var end = error.IndexOf(']');
        if (end <= 1)
        {
            return null;
        }

        return error[1..end];
    }
}
