using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ClipboardHistory.Core;

public static class OcrService
{
    public static async Task<string> RecognizeAsync(byte[] pngBytes)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(pngBytes.AsBuffer());
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var engine = CreateEngine();
            if (engine == null) return "";

            var result = await engine.RecognizeAsync(softwareBitmap);
            return result?.Text ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static async Task<string> GetOrRecognizeAsync(ClipEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.OcrText)) return entry.OcrText;
        if (string.IsNullOrEmpty(entry.ImageFile)) return "";
        var path = Services.Store.ResolveImage(entry.ImageFile);
        if (!File.Exists(path)) return "";
        var text = await RecognizeAsync(File.ReadAllBytes(path));
        if (!string.IsNullOrEmpty(text)) Services.Store.SetOcrText(entry.Id, text);
        return text;
    }

    private static OcrEngine? CreateEngine()
    {
        var langs = Services.Settings?.Current?.OcrLanguages
                    ?? new[] { "zh-Hans", "zh-Hant", "en-US" };
        foreach (var lang in langs)
        {
            if (string.IsNullOrWhiteSpace(lang)) continue;
            try
            {
                var language = new Language(lang.Trim());
                if (OcrEngine.IsLanguageSupported(language))
                {
                    var engine = OcrEngine.TryCreateFromLanguage(language);
                    if (engine != null) return engine;
                }
            }
            catch { }
        }
        try { return OcrEngine.TryCreateFromUserProfileLanguages(); }
        catch { return null; }
    }
}
