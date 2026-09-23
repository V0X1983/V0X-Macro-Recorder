using V0XMacroRecorder.Core.Playback;

namespace V0XMacroRecorder.Tests;

public sealed class ImageSearcherTests
{
    private static CapturedBitmap SolidBitmap(int width, int height, (byte R, byte G, byte B) color)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4] = color.B;
            pixels[i * 4 + 1] = color.G;
            pixels[i * 4 + 2] = color.R;
            pixels[i * 4 + 3] = 255;
        }

        return new CapturedBitmap(width, height, pixels);
    }

    private static void Overlay(CapturedBitmap bitmap, int x, int y, CapturedBitmap patch)
    {
        for (var py = 0; py < patch.Height; py++)
        {
            for (var px = 0; px < patch.Width; px++)
            {
                var srcIndex = (py * patch.Width + px) * 4;
                var dstIndex = ((y + py) * bitmap.Width + (x + px)) * 4;
                for (var c = 0; c < 4; c++)
                {
                    bitmap.PixelsBgra32[dstIndex + c] = patch.PixelsBgra32[srcIndex + c];
                }
            }
        }
    }

    [Fact]
    public void Finds_an_exact_match_at_the_expected_offset()
    {
        var haystack = SolidBitmap(10, 10, (0, 0, 0));
        var needle = SolidBitmap(3, 3, (255, 0, 0));
        Overlay(haystack, 4, 3, needle);

        var result = ImageSearcher.Find(haystack, needle, tolerancePercent: 0);

        Assert.Equal((4, 3), result);
    }

    [Fact]
    public void Tolerance_allows_a_near_match()
    {
        var haystack = SolidBitmap(10, 10, (0, 0, 0));
        var needle = SolidBitmap(3, 3, (255, 0, 0));
        Overlay(haystack, 2, 2, SolidBitmap(3, 3, (245, 10, 5)));

        var result = ImageSearcher.Find(haystack, needle, tolerancePercent: 10);

        Assert.Equal((2, 2), result);
    }

    [Fact]
    public void No_match_returns_null()
    {
        var haystack = SolidBitmap(10, 10, (0, 0, 0));
        var needle = SolidBitmap(3, 3, (255, 0, 0));

        var result = ImageSearcher.Find(haystack, needle, tolerancePercent: 0);

        Assert.Null(result);
    }

    [Fact]
    public void Needle_larger_than_haystack_returns_null()
    {
        var haystack = SolidBitmap(5, 5, (0, 0, 0));
        var needle = SolidBitmap(10, 10, (255, 0, 0));

        var result = ImageSearcher.Find(haystack, needle, tolerancePercent: 0);

        Assert.Null(result);
    }

    [Fact]
    public void Finds_a_match_touching_the_bottom_right_edge()
    {
        var haystack = SolidBitmap(10, 10, (0, 0, 0));
        var needle = SolidBitmap(3, 3, (0, 255, 0));
        Overlay(haystack, 7, 7, needle); // pile dans le coin : 7+3 = 10.

        var result = ImageSearcher.Find(haystack, needle, tolerancePercent: 0);

        Assert.Equal((7, 7), result);
    }

    [Fact]
    public void Finds_the_first_match_in_row_major_order()
    {
        var haystack = SolidBitmap(10, 10, (0, 0, 0));
        var needle = SolidBitmap(2, 2, (1, 2, 3));
        Overlay(haystack, 6, 1, needle);
        Overlay(haystack, 1, 5, needle);

        var result = ImageSearcher.Find(haystack, needle, tolerancePercent: 0);

        Assert.Equal((6, 1), result); // trouvée en premier par un balayage ligne par ligne.
    }
}
