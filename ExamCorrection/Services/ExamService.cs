using ExamCorrection.Clients;
using ExamCorrection.Contracts.TeacherExam;
using ExamCorrection.Entities;
using iText.Barcodes;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.AspNetCore.Hosting;
using Refit;
using System.IO.Compression;

namespace ExamCorrection.Services;

public class ExamService : IExamService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IExamCorrectionClient _examCorrectionClient;
    private readonly IUserContext _userContext;

    public ExamService(
        ApplicationDbContext context,
        IWebHostEnvironment webHostEnvironment,
        IExamCorrectionClient examCorrectionClient,
        IUserContext userContext)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
        _examCorrectionClient = examCorrectionClient;
        _userContext = userContext;
    }

      public async Task<Result> UploadExamPdfAsync(UploadExamRequest request)
    {
        try
        {
            var isExistingTitle = await _context.Exams.AnyAsync(e => e.Title == request.Title);

            if (isExistingTitle)
                return Result.Failure(ExamErrors.DuplicatedExamName);

            // السماح بـ PDF أو صورة
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return Result.Failure(FileErrors.OnlyPdfAllowed);

            var folder = Path.Combine(_webHostEnvironment.WebRootPath, "exams");
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            using (var filePath = new FileStream(fullPath, FileMode.Create))
                await request.File.CopyToAsync(filePath);

            // لو PDF نجيب عدد الصفحات، لو صورة نخليه 1
            int pagesCount = 1;
            if (extension == ".pdf")
                pagesCount = GetPdfPageCount(fullPath);

            // Parse barcode data
            var barcodePositions = new Dictionary<int, (double x, double y)>();
            if (!string.IsNullOrEmpty(request.BarcodeData))
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var parsedData = System.Text.Json.JsonSerializer.Deserialize<List<BarcodePageInfo>>(request.BarcodeData, options);
                    if (parsedData != null)
                    {
                        foreach (var item in parsedData)
                        {
                            barcodePositions[item.Page] = (item.X, item.Y);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing barcode data: {ex.Message}");
                }
            }

            // Get first page coordinates for backward compatibility or defaults
            double defaultX = 0, defaultY = 0;
            if (barcodePositions.TryGetValue(1, out var firstPageCoords))
            {
                defaultX = firstPageCoords.x;
                defaultY = firstPageCoords.y;
            }

            var exam = new Exam
            {
                Title = request.Title,
                Subject = request.Subject,
                PdfPath = $"exams/{fileName}",
                NumberOfPages = pagesCount,
                CreatedAt = DateTime.Now,
                X = defaultX,
                Y = defaultY,
                NameMarkData = request.NameMarkData ?? string.Empty,
                FiducialsData = request.FiducialsData ?? string.Empty,
            };

            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            for (int page = 1; page <= pagesCount; page++)
            {
                double pageX = 0, pageY = 0;
                if (barcodePositions.TryGetValue(page, out var coords))
                {
                    pageX = coords.x;
                    pageY = coords.y;
                }

                _context.ExamPages.Add(new ExamPage
                {
                    ExamId = exam.Id,
                    PageNumber = page,
                    X = pageX,
                    Y = pageY
                });
            }

            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Upload.Exception", ex.ToString(), 500));
        }
    }

    public async Task<Result<IEnumerable<ExamResponse>>> GetAllAsync()
    {
        var exams = await _context.Exams
            .OrderByDescending(e => e.CreatedAt)
            .ProjectToType<ExamResponse>()
            .ToListAsync();

        return Result.Success<IEnumerable<ExamResponse>>(exams);
    }

    public async Task<Result<ExamResponse>> GetAsync(int examId, CancellationToken cancellationToken = default)
    {
        if (await _context.Exams.FindAsync(examId, cancellationToken) is not { } exam)
            return Result.Failure<ExamResponse>(ExamErrors.ExamNotFound);

        return Result.Success(exam.Adapt<ExamResponse>());
    }

    public async Task<Result> DeleteAsync(int examId, CancellationToken cancellationToken = default)
    {
        var existingExam = await _context.Exams.SingleOrDefaultAsync(c => c.Id == examId, cancellationToken);

        if (existingExam is null)
            return Result.Failure(ExamErrors.ExamNotFound);

        _context.Exams.Remove(existingExam);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

  public async Task<Result<FileExamResponse>> GenerateAndDownloadExamsAsync(GenerateExamRequest request)
{
    try
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(x => x.Id == request.ExamId);
        if (exam is null)
            return Result.Failure<FileExamResponse>(ExamErrors.ExamNotFound);

        var students = await _context.Students
            .Where(s => s.ClassId == request.ClassId && !s.IsDisabled)
            .ToListAsync();

        if (!students.Any())
            return Result.Failure<FileExamResponse>(StudentErrors.NoStudentsFound);

        using var outputStream = new MemoryStream();
        using (var writer = new PdfWriter(outputStream))
        using (var destPdf = new PdfDocument(writer))
        {
            var destDoc = new Document(destPdf);

            foreach (var student in students)
            {
                var studentPdfResult = await GenerateForStudent(exam, student, exam.X, exam.Y);
                if (studentPdfResult.IsFailure)
                {
                     return Result.Failure<FileExamResponse>(studentPdfResult.Error);
                }

                using var studentReader = new PdfReader(new MemoryStream(studentPdfResult.Value!));
                using var studentPdfDoc = new PdfDocument(studentReader);
                studentPdfDoc.CopyPagesTo(1, studentPdfDoc.GetNumberOfPages(), destPdf);
            }

            destDoc.Close();
        }

        var finalPdfBytes = outputStream.ToArray();
        return Result.Success(new FileExamResponse(
            File: finalPdfBytes,
            FileName: $"{exam.Title}_{request.ClassId}.pdf",
            ContentType: "application/pdf"
        ));
    }
    catch (Exception ex)
    {
        // سجل الخطأ هنا (يمكنك استخدام ILogger إذا كان متاحاً)
        // _logger.LogError(ex, "خطأ في توليد الامتحانات");
        return Result.Failure<FileExamResponse>(new Error("Exam.GenerationFailed", ex.Message, 500));
    }
}

    private int GetPdfPageCount(string path)
    {
        using var pdf = new PdfDocument(new PdfReader(path));
        return pdf.GetNumberOfPages();
    }

    private async Task<Result<byte[]>> GenerateForStudent(Exam exam, Student student, double x, double y)
    {
        try
        {
            var relativePdfPath = exam.PdfPath.TrimStart('/', '\\');
            var templatePath = Path.Combine(_webHostEnvironment.WebRootPath, relativePdfPath);
            var fontPath = Path.Combine(_webHostEnvironment.WebRootPath, "fonts", "arialbd.ttf");

            if (!File.Exists(templatePath)) 
                 return Result.Failure<byte[]>(ExamErrors.PdfFileNotFound);
            
            if (!File.Exists(fontPath)) 
                 return Result.Failure<byte[]>(ExamErrors.FontNotFound);

        var studentPaper = await _context.StudentExamPapers
            .IgnoreQueryFilters()
            .Include(p => p.Pages)
            .FirstOrDefaultAsync(p => p.ExamId == exam.Id && p.StudentId == student.Id);

        if (studentPaper == null)
        {
            studentPaper = new StudentExamPaper
            {
                ExamId = exam.Id,
                StudentId = student.Id,
                GeneratedAt = DateTime.Now,
                Pages = new List<StudentExamPage>()
            };
            _context.StudentExamPapers.Add(studentPaper);
        }
        else
        {
            _context.StudentExamPages.RemoveRange(studentPaper.Pages);
            studentPaper.Pages.Clear();
        }

        // Fetch exam page coordinates
        var examPages = await _context.ExamPages
            .Where(ep => ep.ExamId == exam.Id)
            .ToDictionaryAsync(ep => ep.PageNumber, ep => new { ep.X, ep.Y });

        var nameMarkPositions = new Dictionary<int, (double x, double y)>();
        if (!string.IsNullOrEmpty(exam.NameMarkData))
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedData = System.Text.Json.JsonSerializer.Deserialize<List<PageCoordinateInfo>>(exam.NameMarkData, options);
                if (parsedData != null)
                {
                    foreach (var item in parsedData) nameMarkPositions[item.Page] = (item.X, item.Y);
                }
            }
            catch {}
        }
        
        var fiducialsPositions = new Dictionary<int, List<(double x, double y)>>();
        if (!string.IsNullOrEmpty(exam.FiducialsData))
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedData = System.Text.Json.JsonSerializer.Deserialize<List<PageCoordinateInfo>>(exam.FiducialsData, options);
                if (parsedData != null)
                {
                    foreach (var item in parsedData) 
                    {
                        if(!fiducialsPositions.ContainsKey(item.Page)) fiducialsPositions[item.Page] = new List<(double, double)>();
                        fiducialsPositions[item.Page].Add((item.X, item.Y));
                    }
                }
            }
            catch {}
        }

        byte[] pdfBytes;
        using (var ms = new MemoryStream())
        {
            using (var writer = new PdfWriter(ms))
            {
                PdfDocument pdf;
                
                bool isPdf = Path.GetExtension(templatePath).ToLower() == ".pdf";

                if (isPdf)
                {
                    var reader = new PdfReader(templatePath);
                    pdf = new PdfDocument(reader, writer);
                }
                else
                {
                    pdf = new PdfDocument(writer);
                    var imageData = iText.IO.Image.ImageDataFactory.Create(templatePath);
                    var image = new Image(imageData);
                    
                    // Create page with exact image dimensions (1:1 pixels to points)
                    var pageSize = new iText.Kernel.Geom.PageSize(imageData.GetWidth(), imageData.GetHeight());
                    pdf.AddNewPage(pageSize);
                    
                    var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdf.GetLastPage());
                    canvas.AddImageAt(imageData, 0, 0, false);
                }

                var doc = new Document(pdf);
                doc.SetMargins(0, 0, 0, 0); // Remove any default margins
                var font = PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);
                
                // Use our manual shaper since we don't have pdfCalligraph license
                string fixedName = ArabicTextShaper.Shape(student.FullName);

                int totalPages = pdf.GetNumberOfPages();
                for (int i = 1; i <= totalPages; i++)
                {
                    var page = pdf.GetPage(i);
                    var cropBox = page.GetCropBox();
                    var rotation = page.GetRotation();
                    var pageSizeWithRotation = page.GetPageSizeWithRotation();
                    float visualWidth = pageSizeWithRotation.GetWidth();
                    float visualHeight = pageSizeWithRotation.GetHeight();
                    
                    // Draw Name Mark using rotation-aware Div
                    if (nameMarkPositions.TryGetValue(i, out var nm))
                    {
                        float visualX = (float)(nm.x * visualWidth);
                        float visualY = (float)(visualHeight - (nm.y * visualHeight) - 20);
                        
                        // Clamp to prevent clipping
                        if (visualX < 0) visualX = 0;
                        if (visualX + 80 > visualWidth) visualX = visualWidth - 80;
                        if (visualY < 0) visualY = 0;
                        if (visualY + 20 > visualHeight) visualY = visualHeight - 20;

                        var nmDiv = new Div()
                            .SetBackgroundColor(iText.Kernel.Colors.ColorConstants.BLACK)
                            .SetFixedPosition(i, visualX, visualY, 80); // 80 as requested
                        nmDiv.SetHeight(20);
                        doc.Add(nmDiv);
                    }
                    
                    // Draw Fiducials using PdfCanvas
                    if (fiducialsPositions.TryGetValue(i, out var fList))
                    {
                        var pdfCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdf);
                        pdfCanvas.SetFillColor(iText.Kernel.Colors.ColorConstants.BLACK);
                        
                        // هنجيب أبعاد الصفحة الحقيقية والمساحة المرئية (CropBox)
                        var box = page.GetCropBox();
                        float pWidth = box.GetWidth();
                        float pHeight = box.GetHeight();

                        // الحجم الموحد للمربعات (بالنقاط - Points)
                        // Calculate size relative to page width (approx 3.4% of page width matches ~20pt on A4)
                        float fSize = (20f / 595f) * pWidth; 
                        
                        foreach (var f in fList)
                        {
                            // 1. تحويل النسب المئوية لأحداثيات فعلية بناءً على العرض والارتفاع المرئي
                            float targetX = (float)(f.x * pWidth);
                            float targetY = (float)(f.y * pHeight);

                            float finalX, finalY;

                            // 2. التعامل مع الدوران (Rotation) بحسابات دقيقة
                            switch (rotation)
                            {
                                case 90:
                                    finalX = box.GetLeft() + targetY;
                                    finalY = box.GetBottom() + targetX;
                                    break;
                                case 180:
                                    finalX = box.GetLeft() + pWidth - targetX - fSize;
                                    finalY = box.GetBottom() + targetY;
                                    break;
                                case 270:
                                    finalX = box.GetLeft() + pWidth - targetY - fSize;
                                    finalY = box.GetBottom() + pHeight - targetX - fSize;
                                    break;
                                default: // 0
                                    finalX = box.GetLeft() + targetX;
                                    finalY = box.GetBottom() + pHeight - targetY - fSize;
                                    break;
                            }

                            // 🚨 إضافة نظام الـ Clamping لضمان عدم خروج المربع عن حدود الصفحة نهائياً
                            if (finalX < box.GetLeft()) finalX = box.GetLeft();
                            if (finalX + fSize > box.GetLeft() + pWidth) finalX = box.GetLeft() + pWidth - fSize;
                            if (finalY < box.GetBottom()) finalY = box.GetBottom();
                            if (finalY + fSize > box.GetBottom() + pHeight) finalY = box.GetBottom() + pHeight - fSize;

                            // رسم المربع (Rectangle بياخد x, y, width, height)
                            pdfCanvas.Rectangle(finalX, finalY, fSize, fSize);
                        }
                        pdfCanvas.Fill();
                    }

                    double pageXPercent = x;
                    double pageYPercent = y;

                    if (examPages.TryGetValue(i, out var pageInfo))
                    {
                        pageXPercent = pageInfo.X;
                        pageYPercent = pageInfo.Y;
                    }

                    float visualBarcodeX = (float)(pageXPercent * visualWidth);
                    float visualBarcodeY = (float)(visualHeight - (pageYPercent * visualHeight) - 38); // Reduced to 38f (two degrees smaller)
                    
                    var barcodeValue = $"{exam.Id}-{student.Id}-{i}";
                    var barcode = new Barcode128(pdf);
                    barcode.SetCode(barcodeValue);
                    barcode.SetBarHeight(38f); // Reduced to 38f as requested
                    barcode.SetFont(null); 
                    barcode.SetX(1.5f);

                    var barcodeForm = barcode.CreateFormXObject(pdf);


                    var img = new Image(barcodeForm)
                        .SetFixedPosition(i, visualBarcodeX, visualBarcodeY)
                        .SetWidth(140f); // Fix width to 140 points so it doesn't grow
                    doc.Add(img);

                    // Student Name (Right above the 38f bars)
                    var namePara = new Paragraph(fixedName)
                        .SetFont(font)
                        .SetFontSize(12) // Slightly smaller for consistency (from 14)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                        .SetFixedPosition(i, visualBarcodeX, visualBarcodeY + 38, 140f); 
                    doc.Add(namePara);

                    // ID Numbers (Centered below the barcode)
                    var idPara = new Paragraph(barcodeValue)
                        .SetFont(font)
                        .SetFontSize(9) // Slightly smaller for consistency (from 10)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                        .SetFixedPosition(i, visualBarcodeX, visualBarcodeY - 12, 140f);
                    doc.Add(idPara);

                    studentPaper.Pages.Add(new StudentExamPage { PageNumber = i, BarcodeValue = barcodeValue });
                }
                doc.Close();
            }
            pdfBytes = ms.ToArray();
        }

        await _context.SaveChangesAsync();
        return Result.Success(pdfBytes);
    }
    catch (Exception ex)
    {
        var fileName = Path.GetFileName(exam.PdfPath);
        Console.WriteLine($"[ExamService Error] Error processing file '{fileName}': {ex.Message}");
        Console.WriteLine($"[ExamService Error] StackTrace: {ex.StackTrace}");
        return Result.Failure<byte[]>(new Error("Exam.GenerationFailed", $"خطأ في معالجة ملف الامتحان '{fileName}': {ex.Message}", 500));
    }
}



    public async Task<Result<List<ExamCorrectionResponse>>> UploadAndSaveExamAnswersAsync(IFormFile file, CancellationToken cancellationToken)
    {
        // Save a copy to AI-Dataset for trainer/model training
        try 
        {
            var datasetFolder = Path.Combine(_webHostEnvironment.WebRootPath, "AI-Dataset");
            if (!Directory.Exists(datasetFolder))
            {
                Directory.CreateDirectory(datasetFolder);
            }

            // Create a unique filename: timestamp_originalName
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var datasetFileName = $"{timestamp}_{file.FileName}";
            var datasetPath = Path.Combine(datasetFolder, datasetFileName);

            using (var streamCopy = new FileStream(datasetPath, FileMode.Create))
            {
                await file.CopyToAsync(streamCopy, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the correction process if saving for AI fails
            Console.WriteLine($"[ExamService] Failed to save file to AI-Dataset: {ex.Message}");
        }

        using var stream = file.OpenReadStream();
        var streamPart = new StreamPart(stream, file.FileName, file.ContentType);

        var response = await _examCorrectionClient.ProcessExamAsync(streamPart, cancellationToken);

        var examResults = new List<ExamCorrectionResponse>();

        foreach (var item in response.Results)
        {
            var examIdParsed = int.TryParse(item.ExamNumber, out var parsedExamId) ? parsedExamId : 0;
            var studentIdParsed = int.TryParse(item.StudentId, out var parsedStudentId) ? parsedStudentId : 0;

            var existing = await _context.StudentExamPapers
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.ExamId == examIdParsed && x.StudentId == studentIdParsed, cancellationToken);

            if (existing is not null)
            {
                existing.FinalScore = item.Score;
                existing.TotalQuestions = item.Total;
                existing.AnnotatedImageUrl = item.AnnotatedImageUrl;
                existing.QuestionDetailsJson = System.Text.Json.JsonSerializer.Serialize(item.Details);

                examResults.Add(new ExamCorrectionResponse
                (
                    examIdParsed,
                    studentIdParsed,
                    item.Score,
                    item.Total,
                    item.Details
                ));

                _context.Update(existing);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(examResults);
    }

    public async Task<Result<TeacherExamResponse>> UploadTeacherExamAsync(UploadTeacherExamRequest request)
    {
        // 1️⃣ التأكد إن ExamId موجود في ExamPage
        var examPageExists = await _context.Exams
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Id == request.ExamId);

        if (!examPageExists)
        {
            return Result.Failure<TeacherExamResponse>(new Error(
                "ExamPage.NotFound",
                $"رقم الامتحان ({request.ExamId}) غير موجود في قاعدة البيانات. يرجى التأكد من إنشاء الامتحان أولاً والحصول على الرقم الصحيح من قائمة الامتحانات.",
                StatusCodes.Status400BadRequest
            ));
        }

        // 2️⃣ التحقق من الملف
        if (request.File == null)
            return Result.Failure<TeacherExamResponse>(new Error(
                "TeacherExam.NoFile",
                "الملف مطلوب",
                StatusCodes.Status400BadRequest
            ));

        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        var ext = Path.GetExtension(request.File.FileName).ToLower();

        if (!allowedExtensions.Contains(ext))
            return Result.Failure<TeacherExamResponse>(new Error(
                "TeacherExam.InvalidFile",
                "الملف يجب أن يكون PDF أو صورة",
                StatusCodes.Status400BadRequest
            ));

        // 3️⃣ حفظ الملف
        var uploadsFolder = Path.Combine(
            _webHostEnvironment.WebRootPath, "Uploads");

        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await request.File.CopyToAsync(stream);

        // 4️⃣ Insert أو Update
        var teacherExam = await _context.TeacherExams
            .FirstOrDefaultAsync(t => t.ExamId == request.ExamId);

        if (teacherExam == null)
        {
            teacherExam = new TeacherExam
            {
                ExamId = request.ExamId,
                PdfPath = filePath,
                QuestionsJson = request.QuestionsJson
            };
            _context.TeacherExams.Add(teacherExam);
        }
        else
        {
            teacherExam.PdfPath = filePath;
            teacherExam.QuestionsJson = request.QuestionsJson;
            teacherExam.CreatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        // 5️⃣ Response
        return Result.Success(new TeacherExamResponse(
            teacherExam.ExamId,
            $"Uploads/{fileName}",
            teacherExam.QuestionsJson
        ));
    }

    private Point MapVisualToPage(float visualX, float visualY, float visualWidth, float visualHeight, int rotation, iText.Kernel.Geom.Rectangle cropBox)
    {
        float x = 0;
        float y = 0;

        // rotation is in degrees (0, 90, 180, 270)
        switch (rotation)
        {
            case 90:
                x = visualY;
                y = visualWidth - visualX;
                break;
            case 180:
                x = visualWidth - visualX;
                y = visualHeight - visualY;
                break;
            case 270:
                x = visualHeight - visualY;
                y = visualX;
                break;
            default: // 0
                x = visualX;
                y = visualY;
                break;
        }

        // Add back the CropBox offset
        return new Point(cropBox.GetLeft() + x, cropBox.GetBottom() + y);
    }

    private record Point(float X, float Y);
}

public record BarcodePageInfo(int Page, double X, double Y);
public record PageCoordinateInfo(int Page, double X, double Y);
