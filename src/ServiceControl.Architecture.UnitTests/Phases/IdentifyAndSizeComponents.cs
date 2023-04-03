namespace ServiceControl.Architecture.UnitTests;

using NUnit.Framework;
using Particular.Approvals;

public abstract class IdentifyAndSizeComponents : ComponentArchitectureTestBase
{
    protected virtual int PercentageOfCodebaseThreshold => 10;
    protected virtual int StdDeviationsInSizeThreshold => 3;


    [Test]
    public void MaintainComponentInventory()
    {
        // Components are identified via namespace and should be added intentionally
        Console.WriteLine($"   {"Component",-60} {"Size",10} {"Files",5}");
        Console.WriteLine(Enumerable.Repeat('-', 80).ToArray());
        foreach (var component in Components!.All.OrderBy(c => c.Name))
        {
            Console.WriteLine($"   {component.Name,-60} {component.Size,10:N0} {component.Files.Count(),5:N0}");
        }

        Approver.Verify(from c in Components!.All orderby c.Name select c.Name, scenario: Folder);
    }

    [Test]
    public void NoComponentShallExceedSomePercentOfTheOverallCodeBase()
    {
        // Components that are too large are difficult to maintain. Consider breaking outliers up into smaller components
        var totalSize = (double)Components!.All.Sum(x => x.Size);

        var outliers = new List<string>();

        Console.WriteLine($"Threshold: More than {PercentageOfCodebaseThreshold}% of the codebase");

        Console.WriteLine($"   {"Component",-60} {"Size",10} {"%",5}");
        Console.WriteLine(Enumerable.Repeat('-', 80).ToArray());
        foreach (var component in Components!.All.OrderBy(c => c.Name))
        {
            var pct = Math.Round(component.Size / totalSize * 100, 2);
            var aboveThreshold = pct > PercentageOfCodebaseThreshold;
            if (aboveThreshold)
            {
                Console.WriteLine($"** {component.Name,-60} {component.Size,10:N0} {pct,5:N} ");
                outliers.Add(component.Name);
            }
            else
            {
                Console.WriteLine($"   {component.Name,-60} {component.Size,10:N0} {pct,5:N} ");
            }
        }

        Approver.Verify(new
        {
            Description = $"Components which are more than {PercentageOfCodebaseThreshold}% of the codebase",
            outliers
        }, scenario: Folder);
    }


    [Test]
    public void NoComponentShallExceedSomeStandardNumberOfDeviationsFromTheMeanComponentSize()
    {
        // Components that are much bigger than the others are good candidates for breaking up
        var totalSize = Components!.All.Sum(x => x.Size);
        var componentCount = Components!.All.Count();
        var meanSize = (double)totalSize / componentCount;
        var stdDev = Math.Sqrt(Components!.All.Sum(c => Math.Pow(c.Size - meanSize, 2)) / (componentCount - 1));

        var outliers = new List<string>();

        Console.WriteLine($"Threshold: {StdDeviationsInSizeThreshold} standard deviations ({stdDev:N2}) from the mean {meanSize:N2}");

        Console.WriteLine($"   {"Component",-60} {"Size",10} {"σ",5}");
        Console.WriteLine(Enumerable.Repeat('-', 80).ToArray());
        foreach (var component in Components!.All.OrderBy(x => x.Name))
        {
            var distance = Math.Round((component.Size - meanSize) / stdDev, 2);
            if (distance > StdDeviationsInSizeThreshold)
            {
                Console.WriteLine($"** {component.Name,-60} {component.Size,10:N0} {distance,5:N}");
                outliers.Add(component.Name);
            }
            else
            {
                Console.WriteLine($"   {component.Name,-60} {component.Size,10:N0} {distance,5:N}");

            }
        }

        Approver.Verify(new
        {
            Description = $"Components which are more than {StdDeviationsInSizeThreshold} standard deviations from the mean in size",
            outliers
        }, scenario: Folder);
    }
}