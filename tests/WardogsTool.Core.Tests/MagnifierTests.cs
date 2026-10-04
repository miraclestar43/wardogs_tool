using WardogsTool.Core.Magnifier;

namespace WardogsTool.Core.Tests;

public class MagnifierTests
{
    [Fact]
    public void Default_zoom_is_2x_and_choices_are_fixed()
    {
        Assert.Equal(2.0, MagnifierGeometry.DefaultZoom);
        Assert.Equal([1.5, 2.0, 2.5, 3.0, 4.0], MagnifierGeometry.ZoomChoices);
    }

    [Fact]
    public void F10_cycles_off_2x_3x_4x_off_one_step_per_press()
    {
        var state = (On: false, Zoom: 4.0); // starting zoom does not matter when off
        var seen = new List<string>();
        for (var press = 0; press < 8; press++)
        {
            state = MagnifierGeometry.NextHotkeyState(state.On, state.Zoom);
            seen.Add(state.On ? $"{state.Zoom:0.0}x" : "OFF");
        }
        Assert.Equal(["2.0x", "3.0x", "4.0x", "OFF", "2.0x", "3.0x", "4.0x", "OFF"], seen);
    }

    [Theory]
    [InlineData(1.5, true, 2.0)]   // UI-only zoom: next larger cycle step
    [InlineData(2.5, true, 3.0)]
    [InlineData(2.0, true, 3.0)]
    [InlineData(3.0, true, 4.0)]
    [InlineData(4.0, false, 4.0)]
    public void F10_from_any_zoom_while_on(double zoom, bool expectedOn, double expectedZoom)
    {
        Assert.Equal((expectedOn, expectedZoom), MagnifierGeometry.NextHotkeyState(true, zoom));
    }

    [Theory]
    [InlineData(2.0, 2.0)]
    [InlineData(4.0, 4.0)]
    [InlineData(2.2, 2.0)]
    [InlineData(9.0, 4.0)]
    [InlineData(0.0, 1.5)]
    [InlineData(double.NaN, 2.0)]
    [InlineData(double.PositiveInfinity, 2.0)]
    public void Stored_zoom_snaps_to_a_choice(double stored, double expected)
    {
        Assert.Equal(expected, MagnifierGeometry.NormalizeZoom(stored));
    }

    [Fact]
    public void Lens_and_source_are_centred_on_the_monitor()
    {
        var layout = MagnifierGeometry.Compute(new PixelRect(0, 0, 1920, 1080), 2.0);
        Assert.Equal(new PixelRect(660, 340, 1260, 740), layout.Lens);      // 600 x 400
        Assert.Equal(new PixelRect(810, 440, 1110, 640), layout.Source);    // 300 x 200
        Assert.Equal(layout.Lens.CenterX, layout.Source.CenterX);
        Assert.Equal(layout.Lens.CenterY, layout.Source.CenterY);
    }

    [Theory]
    [InlineData(1.5, 400, 267)]
    [InlineData(2.0, 300, 200)]
    [InlineData(2.5, 240, 160)]
    [InlineData(3.0, 200, 133)]
    [InlineData(4.0, 150, 100)]
    public void Source_is_lens_divided_by_zoom(double zoom, int width, int height)
    {
        var layout = MagnifierGeometry.Compute(new PixelRect(0, 0, 2560, 1440), zoom);
        Assert.Equal(600, layout.Lens.Width);
        Assert.Equal(400, layout.Lens.Height);
        Assert.Equal(width, layout.Source.Width);
        Assert.Equal(height, layout.Source.Height);
        Assert.Equal(zoom, layout.Zoom);
    }

    [Fact]
    public void Secondary_monitor_left_of_primary()
    {
        // A 1920x1080 monitor at x = -1920 (left of the primary).
        var layout = MagnifierGeometry.Compute(new PixelRect(-1920, 0, 0, 1080), 2.0);
        Assert.Equal(new PixelRect(-1260, 340, -660, 740), layout.Lens);
        Assert.Equal(-960, layout.Lens.CenterX);
        Assert.Equal(-960, layout.Source.CenterX);
    }

    [Fact]
    public void Monitor_above_primary_with_negative_top()
    {
        var layout = MagnifierGeometry.Compute(new PixelRect(0, -1440, 2560, 0), 3.0);
        Assert.Equal(-720, layout.Lens.CenterY);
        Assert.Equal(1280, layout.Source.CenterX);
    }

    [Fact]
    public void Lens_shrinks_to_fit_a_small_monitor()
    {
        var layout = MagnifierGeometry.Compute(new PixelRect(0, 0, 500, 300), 2.0);
        Assert.Equal(new PixelRect(0, 0, 500, 300), layout.Lens);
        Assert.Equal(250, layout.Source.Width);
        Assert.Equal(150, layout.Source.Height);
    }

    [Fact]
    public void Invalid_input_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => MagnifierGeometry.Compute(new PixelRect(0, 0, 0, 0), 2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagnifierGeometry.Compute(new PixelRect(0, 0, 100, 100), 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagnifierGeometry.Compute(new PixelRect(0, 0, 100, 100), double.NaN));
    }
}
