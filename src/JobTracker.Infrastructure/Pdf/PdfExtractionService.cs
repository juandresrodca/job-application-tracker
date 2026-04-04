using System.Text;
using System.Text.RegularExpressions;
using JobTracker.Application.Interfaces;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace JobTracker.Infrastructure.Pdf;

/// <summary>
/// Service for extracting text and structured data from PDF files.
/// Uses iText7 library for reliable .NET PDF text extraction.
/// </summary>
public class PdfExtractionService : IPdfExtractionService
{
    /// <summary>
    /// Extracts all text from a PDF file at the given path.
    /// </summary>
    /// <param name="filePath">Full path to the PDF file.</param>
    /// <returns>Concatenated text from all pages, joined by newlines.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the PDF file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the file is not a valid PDF.</exception>
    public Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        return Task.FromResult(ExtractTextCore(filePath));
    }

    /// <summary>
    /// Attempts to extract the company name from PDF text.
    /// Looks for common patterns like "Company:", "Employer:", or at the beginning.
    /// </summary>
    public Task<string?> ExtractCompanyNameAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        var fullText = ExtractTextCore(filePath);
        var company = ExtractCompanyNameFromText(fullText);
        return Task.FromResult(company);
    }

    /// <summary>
    /// Attempts to extract the job role/title from PDF text.
    /// Looks for common patterns like "Position:", "Role:", "Job Title:", etc.
    /// </summary>
    public Task<string?> ExtractRoleNameAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        var fullText = ExtractTextCore(filePath);
        var role = ExtractRoleNameFromText(fullText);
        return Task.FromResult(role);
    }

    private static string ExtractTextCore(string filePath)
    {
        var sb = new StringBuilder();

        using (var pdfReader = new PdfReader(filePath))
        using (var pdfDocument = new PdfDocument(pdfReader))
        {
            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
            {
                var page = pdfDocument.GetPage(i);
                var strategy = new SimpleTextExtractionStrategy();
                var text = PdfTextExtractor.GetTextFromPage(page, strategy);
                sb.AppendLine(text);
            }
        }

        return sb.ToString().Trim();
    }

    private static string? ExtractCompanyNameFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Pattern 1: Look for "Company: " or "Employer: " followed by text
        var companyMatch = Regex.Match(text, @"(?:Company|Employer|Organization):\s*([^\n]+)", RegexOptions.IgnoreCase);
        if (companyMatch.Success)
        {
            var value = companyMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        // Pattern 2: Look for common company name indicators at the beginning
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0)
        {
            var firstLine = lines[0].Trim();
            // If first line is reasonably short and doesn't look like a heading, it might be company name
            if (firstLine.Length > 2 && firstLine.Length < 100 && !firstLine.Contains("Job Description") && !firstLine.Contains("Position"))
                return firstLine;
        }

        return null;
    }

    private static string? ExtractRoleNameFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Pattern 1: Look for "Position:", "Role:", "Job Title:", "Title:", etc.
        var roleMatch = Regex.Match(text, @"(?:Position|Role|Job\s+Title|Title|Job\s+Name):\s*([^\n]+)", RegexOptions.IgnoreCase);
        if (roleMatch.Success)
        {
            var value = roleMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        // Pattern 2: Look for text after "As a" or "We are looking for" patterns common in job descriptions
        var descMatch = Regex.Match(text, @"(?:position|role|title)\s+(?:of|for)\s+(?:a|an)?\s*([^\n,\.;]+)", RegexOptions.IgnoreCase);
        if (descMatch.Success)
        {
            var value = descMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value) && value.Length < 100)
                return value;
        }

        return null;
    }
}
