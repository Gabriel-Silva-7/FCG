using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace FCG.Domain.Tests;

public class ArchitectureTests
{
    private static readonly string[] ForbiddenPackagePrefixes =
    {
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
    };

    private static string GetDomainCsprojPath([CallerFilePath] string thisFilePath = "")
    {
        var testsDir = Path.GetDirectoryName(thisFilePath)!;
        var repoRoot = Path.GetFullPath(Path.Combine(testsDir, "..", ".."));
        return Path.Combine(repoRoot, "src", "FCG.Domain", "FCG.Domain.csproj");
    }

    [Fact]
    public void Domain_HasNoProjectReferences()
    {
        var csproj = XDocument.Load(GetDomainCsprojPath());

        var projectReferences = csproj.Descendants("ProjectReference")
            .Select(el => el.Attribute("Include")?.Value)
            .ToList();

        Assert.True(
            projectReferences.Count == 0,
            "FCG.Domain não pode referenciar nenhum outro projeto da solução (ADR-001). " +
            "Referências encontradas: " + string.Join(", ", projectReferences));
    }

    [Fact]
    public void Domain_HasNoForbiddenPackageReferences()
    {
        var csproj = XDocument.Load(GetDomainCsprojPath());

        var forbiddenPackages = csproj.Descendants("PackageReference")
            .Select(el => el.Attribute("Include")?.Value)
            .Where(name => name is not null && ForbiddenPackagePrefixes.Any(name.StartsWith))
            .ToList();

        Assert.True(
            forbiddenPackages.Count == 0,
            "FCG.Domain não pode depender de EF Core, ASP.NET Core ou driver de banco (ADR-001). " +
            "Pacotes proibidos encontrados: " + string.Join(", ", forbiddenPackages));
    }
}
