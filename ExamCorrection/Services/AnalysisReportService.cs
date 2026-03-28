using ExamCorrection.Abstractions;
using ExamCorrection.Contracts.Analysis;
using ExamCorrection.Contracts.Reports;
using ExamCorrection.Entities;
using ExamCorrection.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;

namespace ExamCorrection.Services;

public class AnalysisReportService(ApplicationDbContext context, IAnalysisService analysisService, IHttpClientFactory httpClientFactory) : IAnalysisReportService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IAnalysisService _analysisService = analysisService;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportDetailedAnalysisToPdfAsync(DetailedAnalysisPdfRequestDto request)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId);
        if (exam == null)
            return Result.Failure<(byte[] FileContent, string FileName)>(ExamErrors.ExamNotFound);

        // Fetch Papers
        var papersQuery = _context.StudentExamPapers
            .Include(x => x.Student)
            .ThenInclude(x => x.Class)
            .IgnoreQueryFilters()
            .Where(p => p.ExamId == request.ExamId);
            
        if (request.PaperId.HasValue && request.PaperId.Value > 0)
        {
            papersQuery = papersQuery.Where(p => p.Id == request.PaperId.Value);
        }
            
        var papers = await papersQuery.OrderBy(x => x.Student!.FullName).ToListAsync();

        if (!papers.Any())
            return Result.Failure<(byte[] FileContent, string FileName)>(new Error("NoPapers", "No exam papers found for this exam.", StatusCodes.Status404NotFound));

        var goals = await _context.ExamGoals.IgnoreQueryFilters().Where(g => g.ExamId == request.ExamId).ToListAsync();

        using var ms = new MemoryStream();
        using (var writer = new iText.Kernel.Pdf.PdfWriter(ms))
        using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        {
            var document = new iText.Layout.Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(30, 30, 30, 30);
            document.SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
            document.SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);

            var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "arialbd.ttf");
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);

            var currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            var className = papers.FirstOrDefault()?.Student?.Class?.Name ?? "الكل";
            var subjectName = exam.Subject?.Trim() ?? "عام";

            // Modern Color Palette (matching frontend)
            var primaryBlue = new iText.Kernel.Colors.DeviceRgb(17, 85, 204); 
            var accentBlue = new iText.Kernel.Colors.DeviceRgb(59, 130, 246);
            var successGreen = new iText.Kernel.Colors.DeviceRgb(16, 185, 129);
            var dangerRed = new iText.Kernel.Colors.DeviceRgb(239, 68, 68);
            var warningOrange = new iText.Kernel.Colors.DeviceRgb(245, 158, 11);
            var darkGray = new iText.Kernel.Colors.DeviceRgb(75, 85, 99);
            var lightGrayBg = new iText.Kernel.Colors.DeviceRgb(249, 250, 251);
            var borderColor = new iText.Kernel.Colors.DeviceRgb(229, 231, 235);

            // Reusable Header Helper
            Action<string> addPageHeader = (title) => {
                var headerTable = new iText.Layout.Element.Table(3).UseAllAvailableWidth().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetMarginBottom(15);
                
                
                var leftCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT);
                string printDateText = ArabicTextShaper.Shape("تاريخ الطباعة: ") + currentDate;
                leftCell.Add(new iText.Layout.Element.Paragraph(printDateText).SetFont(font).SetFontSize(9));
                headerTable.AddCell(leftCell);

                var midCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "moe_logo.jpg");
                if (File.Exists(logoPath)) {
                    var logoImg = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(logoPath)).SetWidth(60).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);
                    midCell.Add(logoImg);
                }
                midCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(title)).SetFont(font).SetFontSize(14).SetBold().SetFontColor(primaryBlue));
                headerTable.AddCell(midCell);
                
                var rightCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                rightCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("وزارة التعليم")).SetFont(font).SetFontSize(10));
                rightCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("الإدارة العامة للتعليم")).SetFont(font).SetFontSize(10));
                headerTable.AddCell(rightCell);

                document.Add(headerTable);

                // Add Sub-header with details (Subject, Date, Class, School)
                var subHeader = new iText.Layout.Element.Table(4).UseAllAvailableWidth().SetMarginBottom(15).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                
                Action<string, string> addInfo = (lbl, val) => {
                    var cell = new iText.Layout.Element.Cell().SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 0.5f)).SetPadding(3).SetBackgroundColor(lightGrayBg).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
                    var p = new iText.Layout.Element.Paragraph().SetMargin(0).SetMultipliedLeading(1.0f);
                    
                    // Shape label part only to avoid flipping numbers/dates
                    string label = ArabicTextShaper.Shape($"{lbl}: ");
                    p.Add(new iText.Layout.Element.Text(label + val).SetFont(font).SetFontSize(9).SetFontColor(primaryBlue));
                    
                    cell.Add(p);
                    subHeader.AddCell(cell);
                };
                addInfo("التاريخ", exam.CreatedAt.ToString("yyyy-MM-dd"));
                addInfo("المادة", subjectName);
                addInfo("الفصل", className);
                addInfo("المدرسة", "متوسط الامير فيصل"); 
                
                document.Add(subHeader);
            };

            var classReport = _analysisService.GenerateClassReport(papers, goals);

            // Part 1: Class Comprehensive Report (If no specific student selected)
            if (!request.PaperId.HasValue || request.PaperId.Value <= 0)
            {
                addPageHeader("التقرير الشامل للفصل: " + exam.Title);

                // Info Cards Row
                var statsTable = new iText.Layout.Element.Table(4).UseAllAvailableWidth().SetMarginBottom(20);
                Action<string, string, iText.Kernel.Colors.Color> addCard = (lbl, val, clr) => {
                    var cell = new iText.Layout.Element.Cell().SetBorder(new iText.Layout.Borders.SolidBorder(clr, 1)).SetPadding(10).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).SetBackgroundColor(lightGrayBg);
                    cell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(lbl)).SetFont(font).SetFontSize(9).SetFontColor(darkGray));
                    cell.Add(new iText.Layout.Element.Paragraph(val).SetFont(font).SetFontSize(15).SetBold().SetFontColor(clr));
                    statsTable.AddCell(cell);
                };
                addCard("المتوسط العام", $"{classReport.OverallPercentage:F1}%", primaryBlue);
                addCard("بحاجة لدعم", classReport.FailedStudents.ToString(), dangerRed);
                addCard("المجتازين", classReport.PassedStudents.ToString(), successGreen);
                addCard("إجمالي الطلاب", classReport.TotalStudents.ToString(), accentBlue);
                document.Add(statsTable);

                // Bar Chart Image
                if (!string.IsNullOrEmpty(request.BarChartImageBase64)) {
                   document.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(" نظرة عامة على أداء الفصل ")).SetFont(font).SetFontSize(14).SetBold().SetFontColor(primaryBlue).SetMarginBottom(10).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                   try {
                        var b64 = request.BarChartImageBase64.Contains(",") ? request.BarChartImageBase64.Split(',')[1] : request.BarChartImageBase64;
                        var chartImg = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(Convert.FromBase64String(b64))).SetWidth(550).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);
                        document.Add(chartImg.SetMarginBottom(20));
                   } catch {}
                }

                // --- Strengths & Weaknesses (Row) ---
                var swTable = new iText.Layout.Element.Table(2).UseAllAvailableWidth().SetMarginTop(10).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                
                var stCell = new iText.Layout.Element.Cell().SetPadding(10).SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 1)).SetBackgroundColor(lightGrayBg);
                stCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("نقاط القوة")).SetFont(font).SetBold().SetFontColor(successGreen).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                var strengths = classReport.GoalAnalysis.Where(g => g.SuccessRate >= 50).OrderByDescending(g => g.SuccessRate).Take(5);
                foreach (var g in strengths) {
                    string label = ArabicTextShaper.Shape($"• {g.GoalText}");
                    string value = $"({g.SuccessRate:F0}%)";
                    stCell.Add(new iText.Layout.Element.Paragraph(label + " " + value)
                        .SetFont(font).SetFontSize(8).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT));
                }
                
                // Add Class Strength Chart if available
                if (!string.IsNullOrEmpty(request.ClassStrengthRadarImageBase64)) {
                    try {
                        byte[]? imageBytes = GetImageBytes(request.ClassStrengthRadarImageBase64);
                        if (imageBytes != null) {
                            var img = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(imageBytes)).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER).SetWidth(275);
                            stCell.Add(img);
                        }
                    } catch {}
                }
                swTable.AddCell(stCell);

                var wkCell = new iText.Layout.Element.Cell().SetPadding(10).SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 1)).SetBackgroundColor(lightGrayBg);
                wkCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("نقاط الضعف")).SetFont(font).SetBold().SetFontColor(dangerRed).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                var weaknesses = classReport.GoalAnalysis.Where(g => g.SuccessRate < 50).OrderBy(g => g.SuccessRate).Take(5);
                foreach (var g in weaknesses) {
                    string label = ArabicTextShaper.Shape($"• {g.GoalText}");
                    string value = $"({g.SuccessRate:F0}%)";
                    wkCell.Add(new iText.Layout.Element.Paragraph(label + " " + value)
                        .SetFont(font).SetFontSize(8).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT));
                }

                // Add Class Weakness Chart if available
                if (!string.IsNullOrEmpty(request.ClassWeaknessRadarImageBase64)) {
                    try {
                        byte[]? imageBytes = GetImageBytes(request.ClassWeaknessRadarImageBase64);
                        if (imageBytes != null) {
                            var img = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(imageBytes)).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER).SetWidth(275);
                            wkCell.Add(img);
                        }
                    } catch {}
                }
                swTable.AddCell(wkCell);
                document.Add(swTable);

                // --- Part 2: Detailed Question Table (Next Page) ---
                document.Add(new iText.Layout.Element.AreaBreak(iText.Layout.Properties.AreaBreakType.NEXT_PAGE));
                addPageHeader("تقرير تفصيلي للأسئلة");

                // Question Analysis Chart generated via QuickChart
                var questionChartBytes = await GetClassQuestionBarChartAsync(classReport);
                if (questionChartBytes != null) {
                    var img = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(questionChartBytes))
                        .SetWidth(530)
                        .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER)
                        .SetMarginBottom(15);
                    document.Add(img);
                }

                var detailTable = new iText.Layout.Element.Table(new float[] { 1, 5, 1.5f, 1.5f, 1.5f }).UseAllAvailableWidth().SetMarginTop(5).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                string[] headers = { "السؤال", "الهدف / المهارة", "الإجابات", "النسبة %", "التقييم" };
                foreach (var h in headers)
                    detailTable.AddHeaderCell(new iText.Layout.Element.Cell().SetBackgroundColor(primaryBlue).SetFontColor(iText.Kernel.Colors.ColorConstants.WHITE).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(h)).SetFont(font).SetFontSize(10)));

                for (int i = 0; i < classReport.QuestionAnalysis.Count; i++) {
                    var q = classReport.QuestionAnalysis[i];
                    var goal = classReport.GoalAnalysis.FirstOrDefault(ga => ga.QuestionNumbers.Contains(q.QuestionNumber));
                    var goalText = goal?.GoalText ?? "-";
                    var clr = q.SuccessRate >= 80 ? successGreen : q.SuccessRate >= 50 ? warningOrange : dangerRed;
                    var eval = q.SuccessRate >= 80 ? "متقن" : q.SuccessRate >= 50 ? "متوسط" : "متعثر";
                    
                    detailTable.AddCell(new iText.Layout.Element.Cell().SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph((i + 1).ToString()).SetFontSize(9)));
                    detailTable.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(goalText)).SetFont(font).SetFontSize(9)));
                    detailTable.AddCell(new iText.Layout.Element.Cell().SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph($"{q.CorrectCount} / {classReport.TotalStudents}").SetFontSize(9)));
                    detailTable.AddCell(new iText.Layout.Element.Cell().SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph($"{q.SuccessRate:F0}%").SetFont(font).SetBold().SetFontColor(clr)));
                    detailTable.AddCell(new iText.Layout.Element.Cell().SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(eval)).SetFont(font).SetFontSize(9).SetFontColor(clr)));
                }
                document.Add(detailTable);

                // --- Part 3: Individual Reports (Fetch in Parallel then Append) ---
                var studentDataTasks = papers.Select(async paper =>
                {
                    var studentReport = _analysisService.GenerateStudentReport(paper, goals);
                    var studentChart = request.StudentCharts?.FirstOrDefault(c => c.PaperId == paper.Id);

                    // Start chart tasks efficiently
                    var genTask = !string.IsNullOrEmpty(studentChart?.GeneralRadarBase64)
                        ? Task.FromResult<byte[]?>(Convert.FromBase64String(studentChart.GeneralRadarBase64.Split(',').Last()))
                        : GetStudentGeneralRadarChartAsync(studentReport, classReport);
                        
                    var strTask = !string.IsNullOrEmpty(studentChart?.StrengthRadarBase64)
                        ? Task.FromResult<byte[]?>(Convert.FromBase64String(studentChart.StrengthRadarBase64.Split(',').Last()))
                        : GetStudentStrengthsRadarChartAsync(studentReport);
                        
                    var weakTask = !string.IsNullOrEmpty(studentChart?.WeaknessRadarBase64)
                        ? Task.FromResult<byte[]?>(Convert.FromBase64String(studentChart.WeaknessRadarBase64.Split(',').Last()))
                        : GetStudentWeaknessesRadarChartAsync(studentReport);

                    await Task.WhenAll(genTask, strTask, weakTask);

                    return new
                    {
                        Report = studentReport,
                        GeneralChart = await genTask,
                        StrengthChart = await strTask,
                        WeaknessChart = await weakTask
                    };
                }).ToList();

                var studentDataResults = await Task.WhenAll(studentDataTasks);

                foreach (var result in studentDataResults)
                {
                    document.Add(new iText.Layout.Element.AreaBreak(iText.Layout.Properties.AreaBreakType.NEXT_PAGE));
                    AppendStudentReport(document, result.Report, font, primaryBlue, successGreen, dangerRed, lightGrayBg, accentBlue, darkGray, result.GeneralChart, result.StrengthChart, result.WeaknessChart, subjectName);
                }
            }
            else
            {
                // Specific Student Download
                var paper = papers.First();
                addPageHeader("تقرير الأداء الفردي");
                
                var studentReport = _analysisService.GenerateStudentReport(paper, goals);
                
                byte[]? generalChartBytes = GetImageBytes(request.RadarImageBase64);
                byte[]? strengthChartBytes = GetImageBytes(request.StrengthRadarImageBase64);
                byte[]? weaknessChartBytes = GetImageBytes(request.WeaknessRadarImageBase64);

                AppendStudentReport(document, studentReport, font, primaryBlue, successGreen, dangerRed, lightGrayBg, accentBlue, darkGray, generalChartBytes, strengthChartBytes, weaknessChartBytes, subjectName);
            }

            document.Close();
        }

        var fName = $"تحليل_{exam.Title}_{DateTime.Now:yyyyMMdd}.pdf";
        return Result.Success((ms.ToArray(), fName));
    }

    private void AppendStudentReport(iText.Layout.Document document, StudentReportDto studentReport, iText.Kernel.Font.PdfFont font, iText.Kernel.Colors.Color primaryBlue, iText.Kernel.Colors.Color successGreen, iText.Kernel.Colors.Color dangerRed, iText.Kernel.Colors.Color lightGrayBg, iText.Kernel.Colors.Color accentBlue, iText.Kernel.Colors.Color darkGray, byte[]? generalChartBytes, byte[]? strengthChartBytes, byte[]? weaknessChartBytes, string subjectName)
    {
        var roseBg = new iText.Kernel.Colors.DeviceRgb(255, 241, 242);
        var emeraldBg = new iText.Kernel.Colors.DeviceRgb(236, 253, 245);
        var roseText = new iText.Kernel.Colors.DeviceRgb(225, 29, 72);
        var emeraldText = new iText.Kernel.Colors.DeviceRgb(5, 150, 105);
        var borderColor = new iText.Kernel.Colors.DeviceRgb(229, 231, 235);

        // Student Info Card
        var infoBox = new iText.Layout.Element.Table(5).UseAllAvailableWidth().SetMarginBottom(5).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
        Action<string, string, iText.Kernel.Colors.Color> addBit = (l, v, c) => {
            var cell = new iText.Layout.Element.Cell().SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 1)).SetPadding(3).SetBackgroundColor(lightGrayBg).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
            cell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(l)).SetFont(font).SetFontSize(8).SetFontColor(darkGray).SetMargin(0));
            cell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(v)).SetFont(font).SetFontSize(10).SetBold().SetFontColor(c).SetMargin(0));
            infoBox.AddCell(cell);
        };
        addBit("الحالة", studentReport.Status, studentReport.Percentage >= 50 ? successGreen : dangerRed);
        addBit("المادة", subjectName, primaryBlue);
        addBit("الإتقان", $"{studentReport.Percentage:F0}%", studentReport.Percentage >= 50 ? successGreen : dangerRed);
        addBit("الدرجة", $"{studentReport.TotalCorrect} / {studentReport.Answers.Count}", accentBlue);
        addBit("الطالب", studentReport.StudentName, primaryBlue);
        document.Add(infoBox);

        // Main Radar Chart (General Performance)
        if (generalChartBytes != null) {
            try {
                var mainChartTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth().SetMarginBottom(0);
                var cell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).SetPadding(0);
                cell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("الأداء العام للطالب")).SetFont(font).SetFontSize(9).SetBold().SetFontColor(primaryBlue).SetMargin(0));
                cell.Add(new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(generalChartBytes)).SetWidth(280).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER));
                mainChartTable.AddCell(cell);
                document.Add(mainChartTable);

                // Add Legend for Radar Chart
                var legendTable = new iText.Layout.Element.Table(2).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER).SetMarginBottom(3).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                
                var classLegend = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(1).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                var pClassTable = new iText.Layout.Element.Table(2).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.RIGHT);
                pClassTable.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("متوسط الفصل  ")).SetFont(font).SetFontSize(7.5f).SetMargin(0)));
                pClassTable.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).Add(new iText.Layout.Element.Paragraph().SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(244, 114, 182)).SetWidth(12).SetHeight(6).SetMarginLeft(3)));
                classLegend.Add(pClassTable);
                
                var studentLegend = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(1).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                var pStudentTable = new iText.Layout.Element.Table(2).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.RIGHT);
                pStudentTable.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("أداء الطالب  ")).SetFont(font).SetFontSize(7.5f).SetMargin(0)));
                pStudentTable.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).Add(new iText.Layout.Element.Paragraph().SetBackgroundColor(accentBlue).SetWidth(12).SetHeight(6).SetMarginLeft(3)));
                studentLegend.Add(pStudentTable);
                
                legendTable.AddCell(classLegend);
                legendTable.AddCell(studentLegend);
                document.Add(legendTable);
            } catch {}
        }

        // Skills Analysis Section (Strength vs Weakness)
        var strongGoals = studentReport.GoalAnalysis.Where(g => g.SuccessRate >= 50).OrderByDescending(g => g.SuccessRate).ToList();
        var weakGoals = studentReport.GoalAnalysis.Where(g => g.SuccessRate < 50).OrderBy(g => g.SuccessRate).ToList();

        var skillsGrid = new iText.Layout.Element.Table(2).UseAllAvailableWidth().SetMarginBottom(5).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);

        // --- Strong Skills ---
        var strongCell = new iText.Layout.Element.Cell().SetPadding(5).SetBorder(new iText.Layout.Borders.SolidBorder(emeraldText, 0.5f)).SetBackgroundColor(emeraldBg);
        var strongHeader = new iText.Layout.Element.Paragraph().SetMarginBottom(3).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
        string strongHeaderText = $"){strongGoals.Count}( المهارات المتقنة )نقاط القوة(";
        strongHeader.Add(new iText.Layout.Element.Text(ArabicTextShaper.Shape(strongHeaderText)).SetFont(font).SetFontSize(9).SetBold().SetFontColor(emeraldText));
        strongCell.Add(strongHeader);

        foreach (var g in strongGoals) {
            string label = ArabicTextShaper.Shape($"• {g.GoalText}");
            string value = $"({g.SuccessRate:F0}%)";
            var p = new iText.Layout.Element.Paragraph(label + " " + value)
                .SetFont(font).SetFontSize(8).SetFontColor(darkGray).SetMarginBottom(2).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
            strongCell.Add(p);
        }
        if (!strongGoals.Any()) strongCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("لا توجد مهارات متقنة حالياً")).SetFont(font).SetFontSize(8).SetItalic().SetFontColor(darkGray));

        if (strengthChartBytes != null) {
            try {
                strongCell.Add(new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(strengthChartBytes)).SetWidth(160).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER).SetMarginTop(3));
            } catch {}
        }
        skillsGrid.AddCell(strongCell);

        // --- Weak Skills ---
        var weakCell = new iText.Layout.Element.Cell().SetPadding(5).SetBorder(new iText.Layout.Borders.SolidBorder(roseText, 0.5f)).SetBackgroundColor(roseBg);
        var weakHeader = new iText.Layout.Element.Paragraph().SetMarginBottom(3).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
        string weakHeaderText = $"){weakGoals.Count}( المهارات المتعثرة )نقاط الضعف(";
        weakHeader.Add(new iText.Layout.Element.Text(ArabicTextShaper.Shape(weakHeaderText)).SetFont(font).SetFontSize(9).SetBold().SetFontColor(roseText));
        weakCell.Add(weakHeader);



        if (weakGoals.Any()) {
            foreach (var g in weakGoals) {
                string label = ArabicTextShaper.Shape($"• {g.GoalText}");
                string value = $"({g.SuccessRate:F0}%)";
                var p = new iText.Layout.Element.Paragraph(label + " " + value)
                    .SetFont(font).SetFontSize(8.5f).SetFontColor(darkGray).SetMarginBottom(1).SetMultipliedLeading(1.0f).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                weakCell.Add(p);
            }
        } else {
            weakCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("لا توجد مهارات متعثرة تستدعي التدخل")).SetFont(font).SetFontSize(8).SetItalic().SetFontColor(darkGray));
        }

        if (weaknessChartBytes != null) {
            try {
                weakCell.Add(new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(weaknessChartBytes)).SetWidth(150).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER).SetMarginTop(2));
            } catch {}
        }
        skillsGrid.AddCell(weakCell);

        document.Add(skillsGrid);

        // Detailed Remedial Plan Section
        if (weakGoals.Any())
        {
            var remedialTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth().SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT).SetMarginTop(2);
            var remedialCell = new iText.Layout.Element.Cell().SetPadding(5).SetBackgroundColor(roseBg).SetBorder(new iText.Layout.Borders.SolidBorder(roseText, 1));
            
          
            string goalListStr = string.Join(" ، ",
                weakGoals.Select(g => $"{g.GoalText} ({g.SuccessRate:F0}%)"));
            var p = new iText.Layout.Element.Paragraph().SetMultipliedLeading(1.1f).SetMargin(0).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);

            string warning = ArabicTextShaper.Shape("يجب التركيز الفوري على مراجعة الأهداف لكونها نقاط الضعف الأساسي: ");
            string goalList = string.Join(" ، ", weakGoals.Select(g => 
                $"{ArabicTextShaper.Shape(g.GoalText)} ({g.SuccessRate:F0}%)"
            ));
            string fullRemedialText = warning + " " + goalList;
            
            p.Add(new iText.Layout.Element.Text(fullRemedialText)
                .SetFont(font).SetFontSize(8.5f).SetBold().SetFontColor(roseText));

            remedialCell.Add(p);
            remedialTable.AddCell(remedialCell);
            document.Add(remedialTable);
        }
        else
        {
            var remedialTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth().SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT).SetMarginTop(2);
            var remedialCell = new iText.Layout.Element.Cell().SetPadding(5).SetBackgroundColor(lightGrayBg).SetBorder(new iText.Layout.Borders.SolidBorder(accentBlue, 1));
            remedialCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("أداء استثنائي ومتقن! الطالب أظهر إتقاناً لجميع الأهداف. يُنصح بالحفاظ على هذا المستوى الرائع.")).SetFont(font).SetFontSize(8.5f).SetFontColor(emeraldText).SetMargin(0));
            remedialTable.AddCell(remedialCell);
            document.Add(remedialTable);
        }
    }
    
    // QuickChart Chart Generation
    
    private async Task<byte[]?> GetStudentGeneralRadarChartAsync(StudentReportDto studentReport, ClassReportDto classReport)
    {
        if (studentReport.GoalAnalysis == null || !studentReport.GoalAnalysis.Any()) return null;

        var labels = studentReport.GoalAnalysis.Select(g => FormatLabelForChart(g.GoalText)).ToList();
        
        var classAverages = studentReport.GoalAnalysis.Select(stGoal => {
            var classGoal = classReport?.GoalAnalysis?.FirstOrDefault(cg => cg.GoalText == stGoal.GoalText);
            return classGoal != null ? classGoal.SuccessRate : 0;
        }).ToList();
        
        var studentPerformance = studentReport.GoalAnalysis.Select(g => g.SuccessRate).ToList();

        var chartConfig = new
        {
            type = "radar",
            data = new
            {
                labels = labels,
                datasets = new[]
                {
                    new
                    {
                        label = "متوسط الفصل",
                        data = classAverages,
                        backgroundColor = "rgba(244, 114, 182, 0.45)",
                        borderColor = "#f472b6",
                        pointBackgroundColor = "#fff",
                        pointBorderColor = "#f472b6",
                        pointBorderWidth = 2,
                        pointRadius = 4,
                        fill = true,
                        order = 2
                    },
                    new
                    {
                        label = "أداء الطالب",
                        data = studentPerformance,
                        backgroundColor = "rgba(96, 165, 250, 0.45)",
                        borderColor = "#60a5fa",
                        pointBackgroundColor = "#fff",
                        pointBorderColor = "#60a5fa",
                        pointBorderWidth = 2,
                        pointRadius = 4,
                        fill = true,
                        order = 1
                    }
                }
            },
            options = GetCommonRadarOptions()
        };

        return await GenerateChartAsync(chartConfig);
    }

    private async Task<byte[]?> GetStudentStrengthsRadarChartAsync(StudentReportDto studentReport)
    {
        if (studentReport.GoalAnalysis == null) return null;
        var strengths = studentReport.GoalAnalysis.Where(g => g.SuccessRate >= 50).ToList();
        if (!strengths.Any()) return null;

        var chartConfig = new
        {
            type = "radar",
            data = new
            {
                labels = strengths.Select(g => FormatLabelForChart(g.GoalText)).ToList(),
                datasets = new[]
                {
                    new
                    {
                        label = "أداء الطالب (%)",
                        data = strengths.Select(g => g.SuccessRate).ToList(),
                        backgroundColor = "rgba(45, 212, 191, 0.7)",
                        borderColor = "#2dd4bf",
                        pointBackgroundColor = "#2dd4bf",
                        pointBorderColor = "#fff",
                        fill = true
                    }
                }
            },
            options = GetCommonRadarOptions(false)
        };

        return await GenerateChartAsync(chartConfig);
    }

    private async Task<byte[]?> GetStudentWeaknessesRadarChartAsync(StudentReportDto studentReport)
    {
        if (studentReport.GoalAnalysis == null) return null;
        var weaknesses = studentReport.GoalAnalysis.Where(g => g.SuccessRate < 50).ToList();
        if (!weaknesses.Any()) return null;

        var chartConfig = new
        {
            type = "radar",
            data = new
            {
                labels = weaknesses.Select(g => FormatLabelForChart(g.GoalText)).ToList(),
                datasets = new[]
                {
                    new
                    {
                        label = "أداء الطالب (%)",
                        data = weaknesses.Select(g => g.SuccessRate).ToList(),
                        backgroundColor = "rgba(251, 113, 133, 0.4)",
                        borderColor = "#fb7185",
                        borderWidth = 2,
                        pointBackgroundColor = "#fb7185",
                        pointBorderColor = "#fff",
                        fill = true
                    }
                }
            },
            options = GetCommonRadarOptions(false)
        };

        return await GenerateChartAsync(chartConfig);
    }

    private async Task<byte[]?> GenerateChartAsync(object chartConfig)
    {
        try
        {
            var requestBody = new
            {
                backgroundColor = "white",
                width = 500,
                height = 500,
                format = "png",
                version = "3", // Specify Chart.js v3 to use scales.r structure
                chart = chartConfig
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://quickchart.io/chart", content);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            
            Console.WriteLine($"QuickChart Error: {await response.Content.ReadAsStringAsync()}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"QuickChart Exception: {ex.Message}");
            return null;
        }
    }

    private object GetCommonRadarOptions(bool showLegend = false)
    {
        return new
        {
            maintainAspectRatio = false,
            scales = new
            {
                r = new
                {
                    min = 0,
                    max = 100,
                    ticks = new
                    {
                        stepSize = 20,
                        display = true,
                        font = new { size = 10 },
                        color = "#94a3b8",
                        backdropColor = "transparent",
                        z = 1
                    },
                    grid = new { color = "rgba(148,163,184,0.3)" },
                    angleLines = new { color = "rgba(148,163,184,0.3)" },
                    pointLabels = new
                    {
                        font = new { size = 11, weight = "bold" },
                        color = "#64748b"
                    }
                }
            },
            plugins = new
            {
                legend = new { display = showLegend }
            }
        };
    }

    private List<string> FormatLabelForChart(string label)
    {
        // Simple word wrap for QuickChart
        var words = label.Split(' ');
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length > 20)
            {
                string shaped = ArabicTextShaper.Shape(currentLine.Trim());
                char[] charArray = shaped.ToCharArray();
                Array.Reverse(charArray);
                lines.Add(new string(charArray));
                
                currentLine = word + " ";
            }
            else
            {
                currentLine += word + " ";
            }
        }
        
        string finalShaped = ArabicTextShaper.Shape(currentLine.Trim());
        char[] finalArray = finalShaped.ToCharArray();
        Array.Reverse(finalArray);
        lines.Add(new string(finalArray));
        
        return lines;
    }

    private byte[]? GetImageBytes(string? base64String)
    {
        if (string.IsNullOrEmpty(base64String)) return null;
        try {
            // Handle data:image/...;base64, prefix
            var base64Data = base64String.Contains(",") ? base64String.Split(',')[1] : base64String;
            return Convert.FromBase64String(base64Data);
        } catch { return null; }
    }

    private async Task<byte[]?> GetClassQuestionBarChartAsync(ClassReportDto classReport)
    {
        if (classReport.QuestionAnalysis == null || !classReport.QuestionAnalysis.Any()) return null;

        var labels = classReport.QuestionAnalysis.Select(q => q.QuestionDisplay ?? $"س {q.QuestionNumber}").ToList();
        var data = classReport.QuestionAnalysis.Select(q => q.SuccessRate).ToList();
        
        var backgroundColors = classReport.QuestionAnalysis.Select(q => 
            q.SuccessRate >= 80 ? "rgba(34, 197, 94, 0.6)" : 
            q.SuccessRate >= 50 ? "rgba(234, 179, 8, 0.6)" : 
            "rgba(239, 68, 68, 0.6)").ToList();

        var borderColors = classReport.QuestionAnalysis.Select(q => 
            q.SuccessRate >= 80 ? "rgb(34, 197, 94)" : 
            q.SuccessRate >= 50 ? "rgb(234, 179, 8)" : 
            "rgb(239, 68, 68)").ToList();

        var chartConfig = new
        {
            type = "bar",
            data = new
            {
                labels = labels,
                datasets = new[]
                {
                    new
                    {
                        label = "نسبة النجاح (%)",
                        data = data,
                        backgroundColor = backgroundColors,
                        borderColor = borderColors,
                        borderWidth = 1
                    }
                }
            },
            options = new
            {
                legend = new { display = false },
                scales = new
                {
                    yAxes = new[] { new { ticks = new { beginAtZero = true, max = 100 } } }
                }
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(new { chart = JsonSerializer.Serialize(chartConfig), width = 800, height = 400, backgroundColor = "white" });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://quickchart.io/chart", content);
            if (response.IsSuccessStatusCode) return await response.Content.ReadAsByteArrayAsync();
        }
        catch { }
        return null;
    }

    public async Task<Result<(byte[] FileContent, string FileName)>> ExportStudentProgressToPdfAsync(DetailedStudentProgressPdfRequestDto request, string userId)
    {
        using var ms = new MemoryStream();
        using (var writer = new iText.Kernel.Pdf.PdfWriter(ms))
        using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        {
            var document = new iText.Layout.Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(30, 30, 30, 30);
            document.SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
            document.SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);

            var envFontPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "arialbd.ttf");
            if (!System.IO.File.Exists(envFontPath))
            {
                var altPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "fonts", "arialbd.ttf");
                if (System.IO.File.Exists(altPath)) envFontPath = altPath;
            }
            
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(envFontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);

            var primaryGreen = new iText.Kernel.Colors.DeviceRgb(15, 139, 76);
            var successGreen = new iText.Kernel.Colors.DeviceRgb(16, 185, 129);
            var successGreenBg = new iText.Kernel.Colors.DeviceRgb(236, 253, 245); // emerald-50
            var dangerRed = new iText.Kernel.Colors.DeviceRgb(244, 63, 94); // rose-500
            var dangerRedBg = new iText.Kernel.Colors.DeviceRgb(255, 241, 242); // rose-50
            var lightGrayBg = new iText.Kernel.Colors.DeviceRgb(248, 250, 252); // slate-50
            var textSlate = new iText.Kernel.Colors.DeviceRgb(71, 85, 105); // slate-600
            var borderColor = new iText.Kernel.Colors.DeviceRgb(241, 245, 249); // slate-100
            var darkGray = new iText.Kernel.Colors.DeviceRgb(30, 41, 59); // slate-800

            var reportsToGenerate = new List<(Student Student, ExamCorrection.Dtos.Reports.StudentProgressDto Progress, bool HasChart)>();

            if (request.StudentId.HasValue)
            {
                // Individual Student Report
                var student = await _context.Students
                    .Include(s => s.Class)
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == request.StudentId.Value && s.OwnerId == userId);

                if (student == null)
                    return Result.Failure<(byte[] FileContent, string FileName)>(new Error("StudentNotFound", "Student not found.", StatusCodes.Status404NotFound));

                var papers = await _context.StudentExamPapers
                    .Include(p => p.Exam)
                    .IgnoreQueryFilters()
                    .Where(p => p.StudentId == request.StudentId.Value) // Student ownership is already verified above
                    .ToListAsync();

                var examIds = papers.Select(p => p.ExamId).Distinct().ToList();
                var goals = await _context.ExamGoals
                    .IgnoreQueryFilters()
                    .Where(g => examIds.Contains(g.ExamId))
                    .ToListAsync();

                var progress = _analysisService.GenerateStudentProgress(student, papers, goals);
                reportsToGenerate.Add((student, progress, !string.IsNullOrEmpty(request.ProgressChartBase64)));
            }
            else
            {
                // Class Progress Summary Report (Top Students Etc)
                var teacherExamsQuery = _context.Exams.Where(e => e.OwnerId == userId);
                var teacherExamIds = await teacherExamsQuery.Select(e => e.Id).ToListAsync();

                var papers = await _context.StudentExamPapers
                    .IgnoreQueryFilters()
                    .Where(p => teacherExamIds.Contains(p.ExamId) && p.OwnerId == userId)
                    .ToListAsync();

                var studentIdsWithExams = papers.Select(p => p.StudentId).Distinct().ToList();

                var students = await _context.Students
                    .Include(s => s.Class)
                    .IgnoreQueryFilters()
                    .Where(s => studentIdsWithExams.Contains(s.Id) && s.OwnerId == userId)
                    .ToListAsync();

                var goals = await _context.ExamGoals
                    .IgnoreQueryFilters()
                    .Where(g => teacherExamIds.Contains(g.ExamId))
                    .ToListAsync();

                foreach (var stObj in students.OrderBy(s => s.FullName))
                {
                    var studentPapers = papers.Where(p => p.StudentId == stObj.Id).ToList();
                    var progress = _analysisService.GenerateStudentProgress(stObj, studentPapers, goals);
                    reportsToGenerate.Add((stObj, progress, false));
                }

                var summaries = _analysisService.GetStudentsProgressSummary(students, papers, goals);

                // --- Summary Header ---
                var headerTable = new iText.Layout.Element.Table(3).UseAllAvailableWidth().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetMarginBottom(20);
                
                var leftHeader = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.BOTTOM);
                leftHeader.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape($"التاريخ: {DateTime.Now:yyyy-MM-dd}")).SetFont(font).SetFontSize(8).SetFontColor(textSlate));
                headerTable.AddCell(leftHeader);
                
                var midHeader = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
                midHeader.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("تقرير ملخص أداء الطلاب")).SetFont(font).SetFontSize(16).SetBold().SetFontColor(primaryGreen));
                headerTable.AddCell(midHeader);
                
                var rightHeader = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                rightHeader.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("وزارة التعليم")).SetFont(font).SetFontSize(10).SetBold().SetFontColor(darkGray));
                headerTable.AddCell(rightHeader);
                document.Add(headerTable);

                // --- Summary Table ---
                var table = new iText.Layout.Element.Table(new float[] { 3, 1, 1, 1, 2, 2, 1 }).UseAllAvailableWidth().SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                string[] headers = {  "التوجه", "تحتاج دعم","نقاط القوة", "المتوسط", "الاختبارات", "الفصل","اسم الطالب" };
                foreach (var h in headers)
                    table.AddHeaderCell(new iText.Layout.Element.Cell().SetBackgroundColor(primaryGreen).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 1f)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).SetPadding(8).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(h)).SetFont(font).SetFontSize(9).SetBold().SetFontColor(iText.Kernel.Colors.ColorConstants.WHITE)));

                foreach (var s in summaries)
                {
                    var changeText = (s.Change >= 0 ? "+" : "") + s.Change.ToString("F0") + "%";
                    var changeClr = s.Change > 0 ? primaryGreen : s.Change < 0 ? dangerRed : darkGray;
                    table.AddCell(new iText.Layout.Element.Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 0.5f)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph(changeText).SetFont(font).SetBold().SetFontColor(changeClr).SetFontSize(8)));
                    var weaknessesText = string.Join("، ", s.Weaknesses);
                    table.AddCell(new iText.Layout.Element.Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 0.5f)).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(weaknessesText)).SetFont(font).SetFontSize(7).SetFontColor(dangerRed).SetBold()));
        
                    var strengthsText = string.Join("، ", s.Strengths);
                    table.AddCell(new iText.Layout.Element.Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 0.5f)).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(strengthsText)).SetFont(font).SetFontSize(7).SetFontColor(primaryGreen).SetBold()));
                    
                    var avgClr = s.OverallAverage >= 50 ? primaryGreen : dangerRed;
                    table.AddCell(new iText.Layout.Element.Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 0.5f)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph($"{s.OverallAverage:F0}%").SetFont(font).SetBold().SetFontColor(avgClr).SetFontSize(9)));
                    
                    table.AddCell(new iText.Layout.Element.Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 0.5f)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph(s.ExamsTaken.ToString()).SetFontSize(8).SetFontColor(darkGray)));
                    table.AddCell(new iText.Layout.Element.Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 0.5f)).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(s.ClassName)).SetFont(font).SetFontSize(8).SetFontColor(darkGray).SetBold()));
                    table.AddCell(new iText.Layout.Element.Cell().SetPadding(8).SetBorder(new iText.Layout.Borders.SolidBorder(darkGray, 0.5f)).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(s.StudentName)).SetFont(font).SetFontSize(8).SetFontColor(darkGray).SetBold()));

                }
                document.Add(table);
            }

            bool isFirstPage = !request.StudentId.HasValue ? false : true;

            foreach (var report in reportsToGenerate)
            {
                if (!isFirstPage)
                {
                    document.Add(new iText.Layout.Element.AreaBreak(iText.Layout.Properties.AreaBreakType.NEXT_PAGE));
                }
                isFirstPage = false;

                var student = report.Student;
                var progress = report.Progress;

                // --- Header ---
                var headerTable = new iText.Layout.Element.Table(3).UseAllAvailableWidth().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetMarginBottom(25);
                
                var leftCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.BOTTOM);
                var datePara = new iText.Layout.Element.Paragraph().SetMargin(0);
                datePara.Add(new iText.Layout.Element.Text(DateTime.Now.ToString("yyyy-MM-dd")).SetFont(font).SetFontSize(8).SetFontColor(textSlate));
                datePara.Add(new iText.Layout.Element.Text(ArabicTextShaper.Shape("التاريخ: ")).SetFont(font).SetFontSize(8).SetFontColor(textSlate));
                leftCell.Add(datePara);
                headerTable.AddCell(leftCell);
                
                var midCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
                midCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(request.StudentId.HasValue ? "تقرير التطور الأكاديمي للطالب" : "تقرير التطور الأكاديمي الشامل للمجموعة")).SetFont(font).SetFontSize(16).SetBold().SetFontColor(primaryGreen));
                headerTable.AddCell(midCell);
                
                var rightCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
                rightCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("وزارة التعليم")).SetFont(font).SetFontSize(10).SetBold().SetFontColor(darkGray));
                headerTable.AddCell(rightCell);
                
                document.Add(headerTable);

                // --- Student Info Banner (Refined Construction) ---
                var bannerTable = new iText.Layout.Element.Table(3).UseAllAvailableWidth().SetMarginBottom(20).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                Action<string, iText.Layout.Element.IBlockElement, iText.Kernel.Colors.Color> addBannerCell = (lbl, el, clr) => {
                    var cell = new iText.Layout.Element.Cell().SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 0.5f)).SetPadding(12).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.WHITE).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).SetBorderRadius(new iText.Layout.Properties.Radius(5));
                    cell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(lbl)).SetFont(font).SetFontSize(7).SetFontColor(textSlate).SetBold().SetMarginBottom(4));
                    cell.Add(el);
                    bannerTable.AddCell(cell);
                };

                // Cumulative Level
                var levelP = new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(progress.PerformanceLevel)).SetFont(font).SetFontSize(12).SetBold().SetFontColor(progress.OverallAverage >= 50 ? successGreen : dangerRed);
                addBannerCell("المستوى التراكمي", levelP, successGreen);

                // Class / Grade
                var classP = new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(student.Class?.Name ?? "غير محدد")).SetFont(font).SetFontSize(12).SetBold().SetFontColor(primaryGreen);
                addBannerCell("الفصل الدراسي", classP, primaryGreen);

                // Student Name
                var nameP = new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(student.FullName)).SetFont(font).SetFontSize(12).SetBold().SetFontColor(primaryGreen);
                addBannerCell("اسم الطالب", nameP, primaryGreen);

                document.Add(bannerTable);


                // --- Progress Chart ---
                if (report.HasChart)
                {
                    try {
                        byte[]? chartBytes = GetImageBytes(request.ProgressChartBase64);
                        if (chartBytes != null) {
                            document.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("منحنى التطور الأكاديمي")).SetFont(font).SetFontSize(10).SetBold().SetFontColor(darkGray).SetMarginBottom(5));
                            var img = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(chartBytes)).SetWidth(500).SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);
                            document.Add(img.SetMarginBottom(15));
                        }
                    } catch {}
                }

                // --- Visual Skills Evolution (Cards) - Moved Up ---
                var goalMap = progress.ExamSummaries
                    .Where(s => s.GoalAnalysis != null)
                    .SelectMany(s => s.GoalAnalysis.Select(g => new { g.GoalText, g.SuccessRate, ExamTitle = s.ExamTitle, Date = s.Date }))
                    .GroupBy(g => g.GoalText)
                    .Where(g => g.Count() > 1)
                    .Select(g => new {
                        GoalText = g.Key,
                        History = g.OrderBy(x => x.Date)
                                  .Select(x => new { x.SuccessRate, x.ExamTitle, CreatedAt = x.Date })
                                  .ToList()
                    })
                    .OrderByDescending(g => g.History.Count)
                    .ToList();

                if (goalMap.Any())
                {
                    document.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("تطور المهارات ")).SetFont(font).SetFontSize(12).SetBold().SetFontColor(darkGray).SetMarginTop(5));
                    document.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("تحليل استقرار وإتقان المهارات التي تم قياسها في أكثر من تقييم")).SetFont(font).SetFontSize(8).SetFontColor(textSlate).SetMarginBottom(10));
                    
                    var skillsGrid = new iText.Layout.Element.Table(2).UseAllAvailableWidth().SetMarginBottom(15);
                    
                    foreach (var skill in goalMap)
                    {
                        var latest = skill.History.Last();
                        var previous = skill.History.Count > 1 ? skill.History[skill.History.Count - 2] : latest;
                        var diff = latest.SuccessRate - previous.SuccessRate;
                        
                        var cardCell = new iText.Layout.Element.Cell().SetPadding(12).SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 1f)).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.WHITE).SetBorderRadius(new iText.Layout.Properties.BorderRadius(10));
                        
                        var cardHead = new iText.Layout.Element.Table(new float[] { 2, 1 }).UseAllAvailableWidth();
                        cardHead.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(skill.GoalText)).SetFont(font).SetFontSize(9).SetBold().SetFontColor(darkGray)));
                        
                        var diffText = (diff >= 0 ? "▲ +" : "▼ ") + diff.ToString("F0") + "%";
                        var diffClr = diff > 0 ? successGreen : diff < 0 ? dangerRed : textSlate;
                        var diffBg = diff > 0 ? successGreenBg : diff < 0 ? dangerRedBg : lightGrayBg;
                        
                        var badgeContainer = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT);
                        var badge = new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(diffText)).SetFont(font).SetFontSize(6).SetBold().SetFontColor(diffClr).SetBackgroundColor(diffBg).SetPadding(2).SetPaddingLeft(5).SetPaddingRight(5).SetBorderRadius(new iText.Layout.Properties.BorderRadius(4));
                        badgeContainer.Add(badge);
                        badgeContainer.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("تغير النسبة")).SetFont(font).SetFontSize(5).SetFontColor(textSlate));
                        cardHead.AddCell(badgeContainer);
                        cardCell.Add(cardHead);
                        
                        cardCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape("إتقان المهارة حالياً")).SetFont(font).SetFontSize(7).SetFontColor(textSlate).SetMarginTop(8));
                        var rateClr = latest.SuccessRate >= 80 ? successGreen : latest.SuccessRate >= 50 ? primaryGreen : dangerRed;
                        cardCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape($"%{latest.SuccessRate:F0}")).SetFont(font).SetFontSize(13).SetBold().SetFontColor(rateClr).SetMarginBottom(0));
                        
                        float success = (float)latest.SuccessRate;
                        float remaining = 100f - success;
                        var pBar = new iText.Layout.Element.Table(new float[] { success, remaining }).UseAllAvailableWidth().SetMarginTop(3).SetMarginBottom(12);
                        pBar.AddCell(new iText.Layout.Element.Cell().SetHeight(4).SetBackgroundColor(rateClr).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetBorderRadius(new iText.Layout.Properties.BorderRadius(2)));
                        pBar.AddCell(new iText.Layout.Element.Cell().SetHeight(4).SetBackgroundColor(borderColor).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                        cardCell.Add(pBar);
                        
                        var cardFoot = new iText.Layout.Element.Table(skill.History.Count).UseAllAvailableWidth();
                        foreach(var h in skill.History) {
                            var dotCell = new iText.Layout.Element.Cell().SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 0.5f)).SetBackgroundColor(lightGrayBg).SetPadding(3).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).SetBorderRadius(new iText.Layout.Properties.BorderRadius(2));
                            dotCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape($"%{h.SuccessRate:F0}")).SetFont(font).SetFontSize(6).SetFontColor(textSlate).SetBold());
                            cardFoot.AddCell(dotCell);
                        }
                        cardCell.Add(cardFoot);
                        skillsGrid.AddCell(cardCell);
                    }
            
                    document.Add(skillsGrid);
                }

                // --- Headers for Detailed History (Premium Styling) ---
                var historyCols = new float[] { 1.2f, 1.5f, 3.3f };
                var historyHeader = new iText.Layout.Element.Table(historyCols).UseAllAvailableWidth().SetMarginTop(8).SetMarginBottom(5).SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                Action<string, iText.Layout.Properties.TextAlignment> addHeader = (txt, align) => {
                    var hCell = new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetBackgroundColor(lightGrayBg).SetPadding(6).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).SetTextAlignment(align);
                    hCell.Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(txt)).SetFont(font).SetFontSize(8).SetBold().SetFontColor(textSlate));
                    historyHeader.AddCell(hCell);
                };
                addHeader("الدرجة المحرزة", iText.Layout.Properties.TextAlignment.LEFT);
                addHeader("تاريخ الاختبار", iText.Layout.Properties.TextAlignment.CENTER);
                addHeader("اسم الاختبار", iText.Layout.Properties.TextAlignment.RIGHT);
                document.Add(historyHeader);
                
                foreach (var examRec in progress.ExamSummaries.OrderByDescending(e => e.Date))
                {
                    var examContainer = new iText.Layout.Element.Table(1).UseAllAvailableWidth().SetMarginBottom(12).SetBorder(new iText.Layout.Borders.SolidBorder(borderColor, 0.5f)).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.WHITE).SetPadding(0).SetBorderRadius(new iText.Layout.Properties.BorderRadius(8));
                    
                    var scoreClr = examRec.Percentage >= 50 ? successGreen : dangerRed;
                    var scoreBg = examRec.Percentage >= 50 ? successGreenBg : dangerRedBg;
                    var examHead = new iText.Layout.Element.Table(historyCols).UseAllAvailableWidth().SetBaseDirection(iText.Layout.Properties.BaseDirection.RIGHT_TO_LEFT);
                    
                    // Score with Badge styling
                    string scoreValue = $"{examRec.Score}/{examRec.TotalScore} ({examRec.Percentage:F0}%)";
                    var scoreP = new iText.Layout.Element.Paragraph(scoreValue).SetFont(font).SetFontSize(9).SetBold().SetFontColor(scoreClr).SetBackgroundColor(scoreBg).SetPaddingLeft(10).SetPaddingRight(10).SetPaddingTop(4).SetPaddingBottom(4).SetBorderRadius(new iText.Layout.Properties.BorderRadius(10));
                    examHead.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT).Add(scoreP));
                    
                    examHead.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER).Add(new iText.Layout.Element.Paragraph(examRec.Date.ToString("yyyy-MM-dd")).SetFont(font).SetFontSize(8).SetFontColor(textSlate)));
                    examHead.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(examRec.ExamTitle)).SetFont(font).SetFontSize(10).SetBold().SetFontColor(primaryGreen)));
                    
                    examContainer.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(10).Add(examHead));

                    if (examRec.GoalAnalysis != null && examRec.GoalAnalysis.Any())
                    {
                        var examStrengths = examRec.GoalAnalysis.Where(g => g.SuccessRate >= 50).OrderByDescending(g => g.SuccessRate).ToList();
                        var examRecs = examRec.GoalAnalysis.Where(g => g.SuccessRate < 50).OrderBy(g => g.SuccessRate).ToList();

                        var examAnalysisGrid = new iText.Layout.Element.Table(2).UseAllAvailableWidth().SetMarginTop(8);
                        
                        // Strengths for this exam
                        var sCellExam = new iText.Layout.Element.Cell().SetPadding(8).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.WHITE).SetBorderRadius(new iText.Layout.Properties.BorderRadius(5));
                        string strengthHeaderTxt = ArabicTextShaper.Shape($"نقاط القوة ({examStrengths.Count})");
                        sCellExam.Add(new iText.Layout.Element.Paragraph(strengthHeaderTxt).SetFont(font).SetFontSize(8).SetBold().SetFontColor(successGreen).SetBackgroundColor(successGreenBg).SetPadding(3).SetBorderRadius(new iText.Layout.Properties.BorderRadius(3)));
                        foreach(var g in examStrengths) {
                            var row = new iText.Layout.Element.Table(new float[] { 1, 3, 2 }).UseAllAvailableWidth().SetMarginTop(4);
                            row.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).Add(new iText.Layout.Element.Paragraph($"{g.SuccessRate:F0}%").SetFont(font).SetFontSize(7).SetFontColor(successGreen)));
                            float rate = (float)g.SuccessRate;
                            var pb = new iText.Layout.Element.Table(new float[] { rate, 100f-rate }).UseAllAvailableWidth();
                            pb.AddCell(new iText.Layout.Element.Cell().SetHeight(3).SetBackgroundColor(successGreen).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetBorderRadius(new iText.Layout.Properties.BorderRadius(1.5f)));
                            pb.AddCell(new iText.Layout.Element.Cell().SetHeight(3).SetBackgroundColor(borderColor).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                            row.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).Add(pb));
                            row.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(g.GoalText)).SetFont(font).SetFontSize(7).SetFontColor(darkGray)));
                            sCellExam.Add(row);
                        }
                        examAnalysisGrid.AddCell(sCellExam);

                        // Recommendations for this exam
                        var rCellExam = new iText.Layout.Element.Cell().SetPadding(8).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetBackgroundColor(iText.Kernel.Colors.ColorConstants.WHITE).SetBorderRadius(new iText.Layout.Properties.BorderRadius(5));
                        string recHeaderTxt = ArabicTextShaper.Shape($"توصيات للتطوير نقاط الضعف ({examRecs.Count})");
                        rCellExam.Add(new iText.Layout.Element.Paragraph(recHeaderTxt).SetFont(font).SetFontSize(8).SetBold().SetFontColor(dangerRed).SetBackgroundColor(dangerRedBg).SetPadding(3).SetBorderRadius(new iText.Layout.Properties.BorderRadius(3)));
                        foreach(var g in examRecs) {
                            var row = new iText.Layout.Element.Table(new float[] { 1, 3, 2 }).UseAllAvailableWidth().SetMarginTop(4);
                            row.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).Add(new iText.Layout.Element.Paragraph($"%{g.SuccessRate:F0}").SetFont(font).SetFontSize(7).SetFontColor(dangerRed)));
                            float rate = (float)g.SuccessRate;
                            var pb = new iText.Layout.Element.Table(new float[] { rate, 100f-rate }).UseAllAvailableWidth();
                            pb.AddCell(new iText.Layout.Element.Cell().SetHeight(3).SetBackgroundColor(dangerRed).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetBorderRadius(new iText.Layout.Properties.BorderRadius(1.5f)));
                            pb.AddCell(new iText.Layout.Element.Cell().SetHeight(3).SetBackgroundColor(borderColor).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
                            row.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE).Add(pb));
                            row.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT).Add(new iText.Layout.Element.Paragraph(ArabicTextShaper.Shape(g.GoalText)).SetFont(font).SetFontSize(7).SetFontColor(darkGray)));
                            rCellExam.Add(row);
                        }
                        examAnalysisGrid.AddCell(rCellExam);
                        examContainer.AddCell(new iText.Layout.Element.Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).Add(examAnalysisGrid));
                    }
                    document.Add(examContainer);
                }
            }

            document.Close();
        }

        var fileName = "تقرير.pdf";
        if (request.StudentId.HasValue)
        {
             var student = await _context.Students.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == request.StudentId.Value);
             fileName = $"تقرير_{student?.FullName ?? "طالب"}.pdf";
        }
        else
        {
             // For class report, try to get the class name from the students involved
             var teacherExamsQuery = _context.Exams.Where(e => e.OwnerId == userId);
             var teacherExamIds = await teacherExamsQuery.Select(e => e.Id).ToListAsync();
             var papers = await _context.StudentExamPapers.IgnoreQueryFilters().Where(p => teacherExamIds.Contains(p.ExamId) && p.OwnerId == userId).ToListAsync();
             var studentIds = papers.Select(p => p.StudentId).Distinct().ToList();
             var students = await _context.Students.Include(s => s.Class).IgnoreQueryFilters().Where(s => studentIds.Contains(s.Id)).ToListAsync();
             
             var className = students.Select(s => s.Class?.Name).Distinct().ToList();
             var classDisplayName = className.Count == 1 ? className.First() : "طلاب";
             
             fileName = $"ملخص_أداء_{classDisplayName}.pdf";
        }

        return Result.Success((ms.ToArray(), fileName));
    }
}
