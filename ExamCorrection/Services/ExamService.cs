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

                using var studentReader = new PdfReader(new MemoryStream(studentPdfResult.Value));
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
                var font = PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);
                
                // Use our manual shaper since we don't have pdfCalligraph license
                string fixedName = ArabicTextShaper.Shape(student.FullName);

                int totalPages = pdf.GetNumberOfPages();
                for (int i = 1; i <= totalPages; i++)
                {
                    double pageX = x;
                    double pageY = y;

                    if (examPages.TryGetValue(i, out var pageInfo))
                    {
                        pageX = pageInfo.X;
                        pageY = pageInfo.Y;
                    }

                    var barcodeValue = $"{exam.Id}-{student.Id}-{i}";
                    var barcode = new Barcode128(pdf);
                    barcode.SetCode(barcodeValue);
                    barcode.SetBarHeight(40f);
                    barcode.SetX(1.5f);

                    var img = new Image(barcode.CreateFormXObject(pdf))
                        .SetFixedPosition(i, (float)pageX, (float)pageY);
                    doc.Add(img);

                    var namePara = new Paragraph(fixedName)
                        .SetFont(font)
                        .SetFontSize(14)
                        .SetFixedPosition(i, (float)pageX, (float)pageY + 50, 500);
                    doc.Add(namePara);

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
        using var stream = file.OpenReadStream();
        var streamPart = new StreamPart(stream, file.FileName, file.ContentType);

        var response = await _examCorrectionClient.ProcessExamAsync(streamPart, cancellationToken);

        var examResults = new List<ExamCorrectionResponse>();

        foreach (var item in response.Results)
        {
            var examIdParsed = int.TryParse(item.ExamNumber, out var parsedExamId) ? parsedExamId : 0;
            var studentIdParsed = int.TryParse(item.StudentId, out var parsedStudentId) ? parsedStudentId : 0;

            var existing = await _context.StudentExamPapers
                .SingleOrDefaultAsync(x => x.ExamId == examIdParsed && x.StudentId == studentIdParsed, cancellationToken);

            if (existing is not null)
            {
                existing.FinalScore = item.Score;
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
                $"ExamId {request.ExamId} مش موجود فعليًا في قاعدة البيانات اللي الـ API متصلة بيها",
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
}

public record BarcodePageInfo(int Page, double X, double Y);