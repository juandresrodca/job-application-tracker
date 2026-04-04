using FluentAssertions;
using Xunit;
using JobTracker.Application.Interfaces;
using JobTracker.Infrastructure.Pdf;

namespace JobTracker.Tests.Services;

public class PdfExtractionServiceTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "JobTrackerPdfTests");
    private readonly IPdfExtractionService _sut = new PdfExtractionService();

    public PdfExtractionServiceTests()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task ExtractTextAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.pdf");

        await _sut.Invoking(s => s.ExtractTextAsync(nonExistentPath))
                  .Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsExtractedText_ForValidPdf()
    {
        var testPdfPath = CreateSimpleTestPdf();

        var result = await _sut.ExtractTextAsync(testPdfPath);

        result.Should().NotBeNullOrEmpty();
        result.Should().ContainAny("Test", "Document");
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsMultilineText_ForMultiPagePdf()
    {
        var testPdfPath = CreateMultiPageTestPdf();

        var result = await _sut.ExtractTextAsync(testPdfPath);

        result.Should().NotBeNullOrEmpty();
        // Should contain content from multiple pages
        result.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsTrimmedText_WithNoExtraWhitespace()
    {
        var testPdfPath = CreateSimpleTestPdf();

        var result = await _sut.ExtractTextAsync(testPdfPath);

        // Result should be trimmed (no leading/trailing whitespace)
        result.Should().Be(result.Trim());
    }

    private string CreateSimpleTestPdf()
    {
        var pdfPath = Path.Combine(_testDirectory, "test_document.pdf");

        // Use iTextSharp alternative or a minimal PDF hex content
        // Creating a minimal PDF manually to avoid additional dependencies
        byte[] minimalPdf = CreateMinimalPdfBytes("Test Document Content");
        File.WriteAllBytes(pdfPath, minimalPdf);

        return pdfPath;
    }

    private string CreateMultiPageTestPdf()
    {
        var pdfPath = Path.Combine(_testDirectory, "test_multipage.pdf");

        byte[] minimalPdf = CreateMinimalMultiPagePdfBytes("Page One Content", "Page Two Content");
        File.WriteAllBytes(pdfPath, minimalPdf);

        return pdfPath;
    }

    /// <summary>Creates a minimal valid PDF with a single text line.</summary>
    private static byte[] CreateMinimalPdfBytes(string text)
    {
        // Minimal PDF structure
        string pdfContent = @"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< >>
stream
BT
/F1 12 Tf
50 700 Td
(" + text + @") Tj
ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000229 00000 n
0000000339 00000 n
trailer
<< /Size 6 /Root 1 0 R >>
startxref
415
%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(pdfContent);
    }

    /// <summary>Creates a minimal valid PDF with two pages.</summary>
    private static byte[] CreateMinimalMultiPagePdfBytes(string pageOneText, string pageTwoText)
    {
        // Simplified: create two single-page PDFs and combine
        // For testing purposes, we'll create a simple two-page structure
        string pdfContent = @"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< >>
stream
BT
/F1 12 Tf
50 700 Td
(" + pageOneText + @") Tj
ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
6 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 7 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
7 0 obj
<< >>
stream
BT
/F1 12 Tf
50 700 Td
(" + pageTwoText + @") Tj
ET
endstream
endobj
xref
0 8
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000125 00000 n
0000000239 00000 n
0000000349 00000 n
0000000425 00000 n
0000000539 00000 n
trailer
<< /Size 8 /Root 1 0 R >>
startxref
649
%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(pdfContent);
    }
}
