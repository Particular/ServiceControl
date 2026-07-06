namespace ServiceControl.Config.Tests;

using System.Threading;
using NUnit.Framework;
using ServiceControl.Config.Xaml.Controls;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class FormSliderTests
{
    [Test]
    public void Summary_is_in_days_when_value_equals_minimum()
    {
        // Mirrors the order in which the retention slider bindings are applied in XAML
        // (initializer members assign in source order): Minimum coerces Value (0 -> 1) and
        // renders the summary before Units and Value are set. With a stored retention equal
        // to the minimum, the Value assignment causes no further change event, so the summary
        // rendered during coercion is the one the user sees.
        var slider = new FormSlider
        {
            Minimum = 1,
            Units = TimeSpanUnits.Days,
            Value = 1
        };

        Assert.That(slider.Summary, Is.EqualTo("1 Day"));
    }

    [Test]
    public void Summary_refreshes_when_units_change()
    {
        var slider = new FormSlider { Minimum = 2 };

        Assert.That(slider.Summary, Is.EqualTo("2 Days"));

        slider.Units = TimeSpanUnits.Hours;

        Assert.That(slider.Summary, Is.EqualTo("2 Hours"));
    }
}
