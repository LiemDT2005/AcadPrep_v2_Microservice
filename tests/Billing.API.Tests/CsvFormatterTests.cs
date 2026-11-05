using Billing.API.Formatters;
using Xunit;

namespace Billing.API.Tests;

public class CsvFormatterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tTAB", "'\tTAB")]
    [InlineData("=A1,B1", "\"'=A1,B1\"")]
    [InlineData("Nguyễn Văn A", "Nguyễn Văn A")]
    public void EscapesQuotesAndBlocksFormulaInjection(string? value, string expected) =>
        Assert.Equal(expected, CsvOutputFormatter.Escape(value));

    [Fact]
    public void LineUsesCommaAndCrLf() =>
        Assert.Equal("a,,\"b,c\"\r\n", CsvOutputFormatter.Line(["a", null, "b,c"]));
}
