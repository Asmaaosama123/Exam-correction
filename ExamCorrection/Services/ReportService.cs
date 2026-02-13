using ClosedXML.Excel;

using ExamCorrection.Contracts.Reports;
using Microsoft.AspNetCore.Components.Web;
using Razor.Templating.Core;
using System.IO;
using System.Net.WebSockets;

namespace ExamCorrection.Services;

public class ReportService(ApplicationDbContext context) : IReportService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportStudentsToExcelAsync(IEnumerable<int> classIds)
    {
        var students = await _context.Students
           .Include(s => s.Class)
           .Where(s => classIds.Contains(s.ClassId))
           .Select(s => new
           {
               s.FullName,
               s.Email,
               s.MobileNumber,
               ClassName = s.Class!.Name,
               s.IsDisabled,
               s.CreatedAt
           })
           .ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Students");

        var headers = new string[] { "Full Name", "Email", "Mobile Number", "Class", "Status", "Registered On" };

        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).SetValue(headers[i]);

        var headerRange = sheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Font.SetBold();
        headerRange.Style.Font.SetFontSize(14);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (int rowIndex = 0; rowIndex < students.Count; rowIndex++)
        {
            var s = students[rowIndex];
            int excelRow = rowIndex + 2;

            sheet.Cell(excelRow, 1).SetValue(s.FullName);
            sheet.Cell(excelRow, 2).SetValue(s.Email ?? "");
            sheet.Cell(excelRow, 3).SetValue(s.MobileNumber ?? "");
            sheet.Cell(excelRow, 4).SetValue(s.ClassName);
            sheet.Cell(excelRow, 5).SetValue(s.IsDisabled ? "Disabled" : "Active");
            sheet.Cell(excelRow, 6).SetValue(s.CreatedAt.ToString("yyyy-MM-dd"));

            var statusCell = sheet.Cell(excelRow, 5);
            statusCell.Style.Fill.BackgroundColor = s.IsDisabled ? XLColor.Red : XLColor.Green;
        }

        sheet.Columns().AdjustToContents();
        sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.CellsUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.CellsUsed().Style.Border.OutsideBorderColor = XLColor.Black;
        sheet.CellsUsed().Style.Font.SetFontSize(12);

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"Students_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return Result.Success((stream.ToArray(), fileName));
    }

    //public async Task<Result<(byte[] FileContent, string FileName)>> ExportStudentsToPdfAsync(IEnumerable<int> classIds)
    //{
    //    var students = await _context.Students
    //        .Include(s => s.Class)
    //        .Where(s => classIds.Contains(s.ClassId))
    //        .Select(s => new StudentsExportViewModel
    //        {
    //            FullName = s.FullName,
    //            Email = s.Email,
    //            MobileNumber = s.MobileNumber,
    //            ClassName = s.Class!.Name,
    //            IsDisabled = s.IsDisabled,
    //            RegisteredOn = s.CreatedAt
    //        })
    //        .ToListAsync();

    //    var html = await RazorTemplateEngine.RenderAsync("Templates/StudentsTemplate.cshtml", students);

    //    var pdf = Pdf.From(html)
    //        .WithGlobalSetting("PaperSize", "A4")
    //        .WithGlobalSetting("Orientation", "Portrait")
    //        .WithGlobalSetting("DPI", "300")
    //        .WithObjectSetting("WebSettings.DefaultEncoding", "utf-8")
    //        .Content();

    //    var fileName = $"Students_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

    //    return Result.Success((pdf.ToArray(), fileName)); ;
    //}

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportClassesToExcelAsync()
    {
        var classes = await _context.Classes
           .Include(s => s.Students)
           .Select(s => new
           {
               s.Name,
               s.CreatedAt,
               numberOfStudents = s.Students.Count
           })
           .ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("الفصول");

        var headers = new string[] { "الاسم", "تم الإنشاء في", "عدد الطلاب"};

        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).SetValue(headers[i]);

        var headerRange = sheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Font.SetBold();
        headerRange.Style.Font.SetFontSize(14);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (int rowIndex = 0; rowIndex < classes.Count; rowIndex++)
        {
            var s = classes[rowIndex];
            int excelRow = rowIndex + 2;

            sheet.Cell(excelRow, 1).SetValue(s.Name);
            sheet.Cell(excelRow, 2).SetValue(s.CreatedAt.ToString("yyyy-MM-dd"));
            sheet.Cell(excelRow, 3).SetValue(s.numberOfStudents);
        }

        sheet.Columns().AdjustToContents();
        sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.CellsUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.CellsUsed().Style.Border.OutsideBorderColor = XLColor.Black;
        sheet.CellsUsed().Style.Font.SetFontSize(12);

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"الفصول_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return Result.Success((stream.ToArray(), fileName));
    }

    //public async Task<Result<(byte[] FileContent, string FileName)>> ExportClassesToPdfAsync()
    //{
    //    var classes = await _context.Classes
    //       .Include(s => s.Students)
    //       .Select(s => new ClassesExportViewModel
    //       {
    //           Name = s.Name,
    //           CreatedAt = s.CreatedAt,
    //           NumberOfStudents = s.Students.Count
    //       })
    //       .ToListAsync();

    //    var html = await RazorTemplateEngine.RenderAsync("Templates/ClassesTemplate.cshtml", classes);

    //    var pdf = Pdf.From(html)
    //        .WithGlobalSetting("Orientation", "Portrait")
    //        .WithGlobalSetting("DPI", "300")
    //        .WithObjectSetting("WebSettings.DefaultEncoding", "utf-8")
    //        .Content();

    //    var fileName = $"الفصول_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

    //    return Result.Success((pdf.ToArray(), fileName)); ;
    //}
}