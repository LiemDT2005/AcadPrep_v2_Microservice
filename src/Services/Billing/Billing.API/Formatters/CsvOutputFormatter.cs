using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Billing.API.Formatters;

// Custom MVC output formatter selected by content negotiation (Accept: text/csv).
public class CsvOutputFormatter : TextOutputFormatter
{
    public const string MediaType = "text/csv";
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] NeedsQuoting = [',', '"', '\r', '\n'];

    public CsvOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(MediaType));
        // UTF-8 with BOM so Excel shows Vietnamese text correctly.
        SupportedEncodings.Add(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    protected override bool CanWriteType(Type? type) => type is not null && typeof(ICsvExportable).IsAssignableFrom(type);

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var document = (ICsvExportable)context.Object!;
        var response = context.HttpContext.Response;
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            { FileNameStar = document.FileName, FileName = document.FileName }.ToString();
        // The MVC writer never emits a preamble; write the BOM explicitly.
        var preamble = selectedEncoding.GetPreamble();
        if (preamble.Length > 0) await response.Body.WriteAsync(preamble);
        await using var writer = context.WriterFactory(response.Body, selectedEncoding);
        await writer.WriteAsync(Line(document.Header));
        foreach (var row in document.Rows) await writer.WriteAsync(Line(row));
        await writer.FlushAsync();
    }

    public static string Line(IEnumerable<string?> values) => string.Join(',', values.Select(Escape)) + "\r\n";

    // RFC 4180 quoting plus CSV/formula-injection protection (prefix a single quote).
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(FormulaPrefixes, 0, 1) == 0) value = "'" + value;
        return value.IndexOfAny(NeedsQuoting) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
