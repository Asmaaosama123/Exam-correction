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

        var headers = new string[] { "ت", "Full Name", "Email", "Mobile Number", "Class", "Status", "Registered On" };

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

            sheet.Cell(excelRow, 1).SetValue(rowIndex + 1); // Serial Number
            sheet.Cell(excelRow, 2).SetValue(s.FullName);
            sheet.Cell(excelRow, 3).SetValue(s.Email ?? "");
            sheet.Cell(excelRow, 4).SetValue(s.MobileNumber ?? "");
            sheet.Cell(excelRow, 5).SetValue(s.ClassName);
            sheet.Cell(excelRow, 6).SetValue(s.IsDisabled ? "Disabled" : "Active");
            sheet.Cell(excelRow, 7).SetValue(s.CreatedAt.ToString("yyyy-MM-dd"));

            var statusCell = sheet.Cell(excelRow, 6);
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

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportStudentsToPdfAsync(IEnumerable<int> classIds)
    {
        var students = await _context.Students
            .Include(s => s.Class)
            .Where(s => classIds.Contains(s.ClassId))
            .OrderBy(s => s.FullName)
            .ToListAsync();

        using var ms = new MemoryStream();
        using (var writer = new iText.Kernel.Pdf.PdfWriter(ms))
        using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        {
            var document = new iText.Layout.Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(20, 20, 20, 20);

            var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "arialbd.ttf");
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);

            document.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("تقرير الطلاب"))
                .SetFont(font)
                .SetFontSize(20)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            var table = new iText.Layout.Element.Table(6).UseAllAvailableWidth();
            string[] headers = { "ت", "اسم الطالب", "البريد الإلكتروني", "رقم الهاتف", "الفصل", "الحالة" };

            foreach (var header in headers.Reverse())
            {
                table.AddHeaderCell(new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(header)).SetFont(font))
                    .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
            }

            for (int i = 0; i < students.Count; i++)
            {
                var s = students[i];
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(s.IsDisabled ? "معطل" : "نشط")).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(s.Class?.Name ?? "")).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(s.MobileNumber ?? "").SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(s.Email ?? "").SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(s.FullName)).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph((i + 1).ToString()).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
            }

            document.Add(table);
            document.Close();
        }

        var fileName = $"Students_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        return Result.Success((ms.ToArray(), fileName));
    }

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportExamResultsToExcelAsync(int examId)
    {
        var exam = await _context.Exams.FindAsync(examId);
        if (exam == null)
            return Result.Failure<(byte[] FileContent, string FileName)>(ExamErrors.ExamNotFound);

        var results = await _context.StudentExamPapers
            .Include(x => x.Student)
            .ThenInclude(x => x.Class)
            .Where(x => x.ExamId == examId)
            .OrderBy(x => x.Student.FullName)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("نتائج الاختبار");

        var headers = new string[] { "ت", "اسم الطالب", "الفصل", "الدرجة", "المجموع الكلي", "تاريخ التصحيح" };

        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).SetValue(headers[i]);

        var headerRange = sheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Font.SetBold();
        headerRange.Style.Font.SetFontSize(14);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (int rowIndex = 0; rowIndex < results.Count; rowIndex++)
        {
            var res = results[rowIndex];
            int excelRow = rowIndex + 2;

            sheet.Cell(excelRow, 1).SetValue(rowIndex + 1); // Serial Number
            sheet.Cell(excelRow, 2).SetValue(res.Student.FullName);
            sheet.Cell(excelRow, 3).SetValue(res.Student.Class?.Name ?? "");
            sheet.Cell(excelRow, 4).SetValue(res.FinalScore);
            sheet.Cell(excelRow, 5).SetValue(res.TotalQuestions);
            sheet.Cell(excelRow, 6).SetValue(res.GeneratedAt.ToString("yyyy-MM-dd HH:mm"));
        }

        sheet.Columns().AdjustToContents();
        sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.CellsUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.CellsUsed().Style.Border.OutsideBorderColor = XLColor.Black;
        sheet.CellsUsed().Style.Font.SetFontSize(12);

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"{exam.Title}.xlsx";
        foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
        return Result.Success((stream.ToArray(), fileName));
    }

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportExamResultsToPdfAsync(int examId)
    {
        var exam = await _context.Exams.FindAsync(examId);
        if (exam == null)
            return Result.Failure<(byte[] FileContent, string FileName)>(ExamErrors.ExamNotFound);

        var results = await _context.StudentExamPapers
            .Include(x => x.Student)
            .ThenInclude(x => x.Class)
            .Where(x => x.ExamId == examId)
            .OrderBy(x => x.Student.FullName)
            .ToListAsync();

        using var ms = new MemoryStream();
        using (var writer = new iText.Kernel.Pdf.PdfWriter(ms))
        using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        {
            var document = new iText.Layout.Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(20, 20, 20, 20);

            var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "arialbd.ttf");
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);

            // --- Header Table ---
            var headerTable = new iText.Layout.Element.Table(3).UseAllAvailableWidth().SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            // Right Cell (Saudi Arabia labels)
            var rightCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
            rightCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("المملكة العربية السعودية")).SetFont(font).SetFontSize(10));
            rightCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("وزارة التعليم")).SetFont(font).SetFontSize(10));
            rightCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("الإدارة العامة للتعليم")).SetFont(font).SetFontSize(10));
            rightCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("مدرسة: .....................")).SetFont(font).SetFontSize(10));
            headerTable.AddCell(rightCell);

            // Middle Cell (Logo and Title)
            var midCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
            
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo-no-bg.png");
            if (File.Exists(logoPath))
            {
                var logoData = iText.IO.Image.ImageDataFactory.Create(logoPath);
                var logoImg = new iText.Layout.Element.Image(logoData).SetWidth(60).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);
                midCell.Add(logoImg);
            }
            midCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("وزارة التعليم")).SetFont(font).SetFontSize(10));
            midCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape($"كشف رصد درجات مادة {exam.Subject} للفصل ........")).SetFont(font).SetFontSize(12).SetBold());
            headerTable.AddCell(midCell);

            // Left Cell (Exam Details)
            var leftCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT);
            leftCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("الصف: .....................")).SetFont(font).SetFontSize(10));
            leftCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("القسم: .....................")).SetFont(font).SetFontSize(10));
            leftCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("الفصل: .....................")).SetFont(font).SetFontSize(10));
            leftCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape($"المادة: {exam.Subject}")).SetFont(font).SetFontSize(10));
            headerTable.AddCell(leftCell);

            document.Add(headerTable);
            document.Add(new iText.Layout.Element.Paragraph("\n"));

            var table = new iText.Layout.Element.Table(5).UseAllAvailableWidth();
            string[] headers = { "ت", "اسم الطالب", "الفصل", "الدرجة", "المجموع الكلي" };

            foreach (var header in headers.Reverse())
            {
                table.AddHeaderCell(new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(header)).SetFont(font))
                    .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
            }

            for (int i = 0; i < results.Count; i++)
            {
                var res = results[i];
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(res.TotalQuestions.ToString()).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(res.FinalScore.ToString()).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(res.Student.Class?.Name ?? "")).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(res.Student.FullName)).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph((i + 1).ToString()).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
            }

            document.Add(table);
            document.Close();
        }

        var fileName = $"{exam.Title}.pdf";
        foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
        return Result.Success((ms.ToArray(), fileName));
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

        var headers = new string[] { "ت", "الاسم", "تم الإنشاء في", "عدد الطلاب"};

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

            sheet.Cell(excelRow, 1).SetValue(rowIndex + 1); // Serial Number
            sheet.Cell(excelRow, 2).SetValue(s.Name);
            sheet.Cell(excelRow, 3).SetValue(s.CreatedAt.ToString("yyyy-MM-dd"));
            sheet.Cell(excelRow, 4).SetValue(s.numberOfStudents);
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

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportClassesToPdfAsync()
    {
        var classes = await _context.Classes
           .Include(s => s.Students)
           .OrderBy(s => s.Name)
           .ToListAsync();

        using var ms = new MemoryStream();
        using (var writer = new iText.Kernel.Pdf.PdfWriter(ms))
        using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        {
            var document = new iText.Layout.Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(20, 20, 20, 20);

            var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "arialbd.ttf");
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);

            document.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("تقرير الفصول"))
                .SetFont(font)
                .SetFontSize(20)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            var table = new iText.Layout.Element.Table(4).UseAllAvailableWidth();
            string[] headers = { "ت", "اسم الفصل", "عدد الطلاب", "تاريخ الإنشاء" };

            foreach (var header in headers.Reverse())
            {
                table.AddHeaderCell(new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(header)).SetFont(font))
                    .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
            }

            for (int i = 0; i < classes.Count; i++)
            {
                var c = classes[i];
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(c.CreatedAt.ToString("yyyy-MM-dd")).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(c.Students.Count.ToString()).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(c.Name)).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                table.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph((i + 1).ToString()).SetFont(font)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
            }

            document.Add(table);
            document.Close();
        }

        var fileName = $"Classes_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        return Result.Success((ms.ToArray(), fileName));
    }
}