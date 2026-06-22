#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run SenseSpaceMan.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Loads space-man-sprites.jpg, knocks out the background color so
// the sprites stand on transparency, then runs
// TextureCatalog.Sense() and visualizes the detected regions by
// drawing outlines over the original image.

using Blitter;
using Blitter.Bits;

const int DesignW = 1280;
const int DesignH = 720;

// JPEG has no alpha, so the background is the JPEG's solid color.
// SetAlpha replaces every pixel within tolerance of the top-left
// pixel's color with alpha = 0, making Sense's transparency-based
// gutter detection work. JPEG halos around sprite edges still leave
// stray almost-background pixels; the minRegion thresholds on Sense
// then drop tiny noise runs.
Application.Current.SetCallerAssetFolder();
var sheet = Bitmap.Load("space-man-sprites.png");
//sheet.SetAlpha(0, sheet.GetPixel(0, 0), tolerance: 30);
//sheet.Save("space-man-sprites.png");

var atlas = TextureCatalog.Sense(
    sheet,
    minRegionWidth: 8,
    minRegionHeight: 8,
    minRowGutter: 4,
    minColumnGutter: 4
    );

var window = new Window2D(DesignW, DesignH)
{
    Title = $"Sense: {atlas.Count} regions detected",
    BackgroundColor = new Color(100, 100, 120),
    CloseKey = Key.Escape,
    LogicalSize = (DesignW, DesignH),
};

// Fit the sheet to the design surface preserving aspect.
var (sw, sh) = sheet.Size;
float scale = MathF.Min(DesignW / (float)sw, DesignH / (float)sh);
float drawW = sw * scale;
float drawH = sh * scale;
float drawX = (DesignW - drawW) * 0.5f;
float drawY = (DesignH - drawH) * 0.5f;
var dst = new Rect(drawX, drawY, drawW, drawH);

window.Rendering += (w, rd) =>
{
    rd.DrawImage(sheet, dst);

    // Outline every detected region in the catalog's source-image
    // coordinates, mapped onto the displayed destination rect.
    for (int i = 0; i < atlas.Count; i++)
    {
        var src = ((ITextureRegion)atlas[i]).Region;
        float x0 = drawX + src.X * scale;
        float y0 = drawY + src.Y * scale;
        float x1 = x0 + src.Width * scale;
        float y1 = y0 + src.Height * scale;

        rd.DrawColor = new Color(0, 255, 80);
        rd.DrawLine(x0, y0, x1, y0);
        rd.DrawLine(x1, y0, x1, y1);
        rd.DrawLine(x1, y1, x0, y1);
        rd.DrawLine(x0, y1, x0, y0);

        // Translucent backdrop + label so the index stays readable
        // over whatever sprite pixels are underneath.
        var label = i.ToString();
        var labelBg = new Rect(x0 + 2, y0 + 2, 8f * label.Length + 4, 12);
        rd.DrawColor = new Color(0, 0, 0, 140);
        rd.DrawFillRect(labelBg);
        rd.DrawColor = new Color(255, 255, 255, 220);
        rd.DrawDebugText((int)(x0 + 4), (int)(y0 + 4), label);
    }

    rd.DrawColor = new Color(220, 230, 245);
    rd.DrawDebugText(20, 20, $"{atlas.Count} regions");
};

await window.WaitForCloseAsync();
