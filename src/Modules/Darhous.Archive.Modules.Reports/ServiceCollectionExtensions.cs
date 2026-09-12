using Darhous.Archive.Modules.Reports.Audit;
using Darhous.Archive.Modules.Reports.Exporting;
using Darhous.Archive.Modules.Reports.Printing;
using Darhous.Archive.Modules.Reports.SavedViews;
using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Modules.Reports;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddSingleton<IReportExporter, CsvExporter>();
        services.AddSingleton<IReportExporter, ExcelExporter>();
        services.AddSingleton<IReportExporter, PdfExporter>();
        services.AddSingleton<IReportExportService, ReportExportService>();
        services.AddSingleton<IAuditReportService, AuditReportService>();
        services.AddSingleton<ISavedViewReportService, SavedViewReportService>();
        services.AddSingleton<IPrintProcessLauncher, WindowsPrintProcessLauncher>();
        services.AddSingleton<IPdfPrintService, PdfPrintService>();

        return services;
    }
}
