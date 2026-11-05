using System.Xml.Linq;

namespace Demeure.Tests.Architecture;

public sealed class LayerDependencyTests
{
    [Theory]
    [InlineData("Demeure.Domain", new string[0])]
    [InlineData("Demeure.Application", new[] { "Demeure.Domain" })]
    [InlineData("Demeure.Infrastructure", new[] { "Demeure.Application", "Demeure.Domain" })]
    [InlineData("Demeure.Api", new[] { "Demeure.Application", "Demeure.Infrastructure" })]
    public void ProjectsReferenceOnlyTheirAllowedLayers(string project, string[] allowedReferences)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Demeure.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var projectPath = Path.Combine(directory.FullName, "backend", project, $"{project}.csproj");
        var document = XDocument.Load(projectPath);
        var references = document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(allowedReferences.Order(StringComparer.Ordinal), references);
        if (project == "Demeure.Domain")
        {
            Assert.Empty(document.Descendants("PackageReference"));
            Assert.Empty(document.Descendants("FrameworkReference"));
        }
    }
}
