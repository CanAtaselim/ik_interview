using Recruitment.Application.Features.Reports;

namespace Recruitment.Infrastructure.Reports;

internal sealed class ReportRenderer : IReportRenderer
{
    private const string PdfType = "application/pdf";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ReportFile Render(ReportDocument document, ReportFormat format, string fileName) =>
        format == ReportFormat.Pdf
            ? new ReportFile($"{fileName}.pdf", PdfType, PdfReportWriter.Write(document))
            : new ReportFile($"{fileName}.xlsx", XlsxType, ExcelReportWriter.Write(document));
}
