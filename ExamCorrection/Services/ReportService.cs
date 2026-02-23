using ClosedXML.Excel;

using ExamCorrection.Contracts.Reports;
using Microsoft.AspNetCore.Components.Web;
using Razor.Templating.Core;
using System.IO;
using System.Net.WebSockets;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.Layout.Properties;
using iText.IO.Font;
using Microsoft.AspNetCore.Hosting;

namespace ExamCorrection.Services;

public class ReportService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment) : IReportService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;

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

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportExamResultsToExcelAsync(int examId)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
        if (exam == null)
            return Result.Failure<(byte[] FileContent, string FileName)>(new Error("ExamNotFound", "الاختبار غير موجود", 404));

        var results = await _context.StudentExamPapers
            .Include(p => p.Student)
                .ThenInclude(s => s.Class)
            .Where(p => p.ExamId == examId)
            .Select(p => new
            {
                StudentName = p.Student.FullName,
                ClassName = p.Student.Class.Name,
                Score = p.FinalScore,
                TotalQuestions = p.TotalQuestions,
                GeneratedAt = p.GeneratedAt
            })
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("نتائج الاختبار");

        sheet.RightToLeft = true;

        // --- Header Section ---
        // Right Section (School Info)
        sheet.Cell(1, 1).SetValue("المملكة العربية السعودية");
        sheet.Cell(2, 1).SetValue("وزارة التعليم");
        sheet.Cell(3, 1).SetValue("الإدارة العامة للتعليم بمنطقة الباحة");
        sheet.Cell(4, 1).SetValue("متوسطة الأمير فيصل بالعقيق");

        // Center Section (Title)
        var titleCell = sheet.Cell(3, 3);
        titleCell.SetValue($"كشف رصد درجات مادة {exam.Subject} للفصل");
        titleCell.Style.Font.SetBold().Font.SetFontSize(16).Font.SetColor(XLColor.FromHtml("#01172f"));
        titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(3, 3, 3, 4).Merge();

        // Left Section (Exam Details)
        string className = results.FirstOrDefault()?.ClassName ?? "---";
        sheet.Cell(1, 5).SetValue($"الصف: {className}");
        sheet.Cell(2, 5).SetValue("القسم: بنين");
        sheet.Cell(3, 5).SetValue("العام: 1446-1447هـ");
        sheet.Cell(4, 5).SetValue("الفصل الدراسي: الثاني");
        sheet.Cell(5, 5).SetValue($"المادة: {exam.Subject}");

        var headerInfoRange = sheet.Range(1, 1, 5, 5);
        headerInfoRange.Style.Font.SetFontSize(11);
        headerInfoRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // --- Table Headers ---
        var tableStartRow = 7;
        var headers = new string[] { "م", "اسم الطالب", "الدرجة النهائية", "عدد الأسئلة", "تاريخ التصحيح" };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(tableStartRow, i + 1);
            cell.SetValue(headers[i]);
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0"); // Light blue-gray
            cell.Style.Font.SetBold();
            cell.Style.Font.SetFontSize(12);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // --- Student Data ---
        for (int rowIndex = 0; rowIndex < results.Count; rowIndex++)
        {
            var r = results[rowIndex];
            int excelRow = tableStartRow + rowIndex + 1;

            sheet.Cell(excelRow, 1).SetValue(rowIndex + 1);
            sheet.Cell(excelRow, 2).SetValue(r.StudentName);
            sheet.Cell(excelRow, 3).SetValue(r.Score ?? 0);
            sheet.Cell(excelRow, 4).SetValue(r.TotalQuestions ?? 0);
            sheet.Cell(excelRow, 5).SetValue(r.GeneratedAt.ToString("yyyy-MM-dd HH:mm"));

            var dataRange = sheet.Range(excelRow, 1, excelRow, headers.Length);
            dataRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Cell(excelRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; // Name aligned right
        }

        sheet.Columns().AdjustToContents();
        sheet.Column(2).Width = 40; // Fixed width for name column

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"{exam.Title}_الدرجات_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return Result.Success((stream.ToArray(), fileName));
    }

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportExamResultsToPdfAsync(int examId)
    {
        try
        {
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
            if (exam == null)
                return Result.Failure<(byte[] FileContent, string FileName)>(new Error("ExamNotFound", "الاختبار غير موجود", 404));

            var results = await _context.StudentExamPapers
                .Include(p => p.Student)
                    .ThenInclude(s => s.Class)
                .Where(p => p.ExamId == examId)
                .Select(p => new
                {
                    StudentName = p.Student.FullName,
                    ClassName = p.Student.Class.Name,
                    Score = p.FinalScore,
                    TotalQuestions = p.TotalQuestions,
                    GeneratedAt = p.GeneratedAt
                })
                .ToListAsync();

            using var stream = new MemoryStream();
            var writer = new PdfWriter(stream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);

            var fontPath = Path.Combine(_webHostEnvironment.WebRootPath, "fonts", "arialbd.ttf");
            if (!File.Exists(fontPath))
                return Result.Failure<(byte[] FileContent, string FileName)>(new Error("FontNotFound", $"Font file not found at: {fontPath}", 500));

            var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);

            document.SetFont(font);

            // Header Table (3 columns)
            var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 35, 30, 35 }))
                .UseAllAvailableWidth()
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            // Right Column (School Info)
            var rightText = "المملكة العربية السعودية\nوزارة التعليم\nالإدارة العامة للتعليم بمنطقة الباحة\nمتوسطة الأمير فيصل بالعقيق";
            var shapedRightText = string.Join("\n", rightText.Split('\n').Select(ArabicTextShaper.Shape));
            var rightCell = new Cell().Add(new Paragraph(shapedRightText)
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.RIGHT))
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            headerTable.AddCell(rightCell);

            // Center Column (Logo Placeholder)
            var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "logo-no-bg.png");
            Cell centerCell;
            if (File.Exists(logoPath))
            {
                var logoData = iText.IO.Image.ImageDataFactory.Create(logoPath);
                var logo = new iText.Layout.Element.Image(logoData).SetHorizontalAlignment(HorizontalAlignment.CENTER).SetHeight(50);
                centerCell = new Cell().Add(logo).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            }
            else
            {
                centerCell = new Cell().Add(new Paragraph(ArabicTextShaper.Shape("وزارة التعليم"))
                    .SetFontSize(12).SetFontColor(iText.Kernel.Colors.ColorConstants.GRAY).SetTextAlignment(TextAlignment.CENTER))
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            }
            headerTable.AddCell(centerCell);

            // Left Column (Exam Info)
            string className = results.FirstOrDefault()?.ClassName ?? "---";
            var leftText = $"الصف: {className}\nالقسم: بنين\nالعام: 1446-1447هـ\nالفصل الدراسي: الثاني\nالمادة: {exam.Subject}";
            var shapedLeftText = string.Join("\n", leftText.Split('\n').Select(ArabicTextShaper.Shape));
            var leftCell = new Cell().Add(new Paragraph(shapedLeftText)
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.RIGHT))
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            headerTable.AddCell(leftCell);

            document.Add(headerTable);

            // Title
            var title = new Paragraph(ArabicTextShaper.Shape($"كشف رصد درجات مادة {exam.Subject} للفصل"))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(16)
                .SetBold()
                .SetMarginTop(10);
            document.Add(title);

            document.Add(new Paragraph("\n"));

            var table = new Table(UnitValue.CreatePercentArray(new float[] { 10, 30, 15, 15, 30 }))
                .UseAllAvailableWidth();

            string[] headers = { "م", "اسم الطالب", "الدرجة النهائية", "عدد الأسئلة", "تاريخ التصحيح" };
            foreach (var h in headers)
            {
                table.AddHeaderCell(new Cell().Add(new Paragraph(ArabicTextShaper.Shape(h)))
                    .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(TextAlignment.CENTER));
            }

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                table.AddCell(new Cell().Add(new Paragraph((i + 1).ToString()))
                    .SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph(ArabicTextShaper.Shape(r.StudentName)))
                    .SetTextAlignment(TextAlignment.RIGHT));
                table.AddCell(new Cell().Add(new Paragraph((r.Score ?? 0).ToString()))
                    .SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph((r.TotalQuestions ?? 0).ToString()))
                    .SetTextAlignment(TextAlignment.CENTER));
                table.AddCell(new Cell().Add(new Paragraph(r.GeneratedAt.ToString("yyyy-MM-dd HH:mm")))
                    .SetTextAlignment(TextAlignment.CENTER));
            }

            document.Add(table);
            document.Close();

            var fileName = $"{exam.Title}_الدرجات_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return Result.Success((stream.ToArray(), fileName));
        }
        catch (Exception ex)
        {
            return Result.Failure<(byte[] FileContent, string FileName)>(new Error("PdfGenerationError", $"An error occurred during PDF generation: {ex.Message}", 500));
        }
    }
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