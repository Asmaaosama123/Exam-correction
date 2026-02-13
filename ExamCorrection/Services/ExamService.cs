using ExamCorrection.Clients;
using ExamCorrection.Contracts.TeacherExam;
using ExamCorrection.Entities;
//using ExamCorrection.Records;
using iText.Barcodes;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.AspNetCore.Hosting;
using Refit;
using System.IO.Compression;

namespace ExamCorrection.Services;

public class ExamService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment,
    IExamCorrectionClient examCorrectionClient) : IExamService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;
    private readonly IExamCorrectionClient _examCorrectionClient = examCorrectionClient;

    public async Task<Result> UploadExamPdfAsync(UploadExamRequest request)
    {
        var isExistingTitle = await _context.Exams.AnyAsync(e => e.Title == request.Title);

        if (isExistingTitle)
            return Result.Failure<Exam>(ExamErrors.DuplicatedExamName);

        // السماح بـ PDF أو صورة
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return Result.Failure<Exam>(FileErrors.OnlyPdfAllowed);

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

        var exam = new Exam
        {
            Title = request.Title,
            Subject = request.Subject,
            PdfPath = $"exams/{fileName}",
            NumberOfPages = pagesCount,
            CreatedAt = DateTime.Now,
            X = request.X,
            Y = request.Y,
        };

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();

        for (int page = 1; page <= pagesCount; page++)
        {
            _context.ExamPages.Add(new ExamPage
            {
                ExamId = exam.Id,
                PageNumber = page
            });
        }

        await _context.SaveChangesAsync();

        return Result.Success();
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
        var exam = await _context.Exams.FirstOrDefaultAsync(x => x.Id == request.ExamId);
        if (exam is null)
            return Result.Failure<FileExamResponse>(ExamErrors.ExamNotFound);

        var students = await _context.Students
            .Where(s => s.ClassId == request.ClassId && !s.IsDisabled)
            .ToListAsync();

        if (!students.Any())
            return Result.Failure<FileExamResponse>(StudentErrors.NoStudentsFound);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var student in students)
            {
                var pdfBytes = await GenerateForStudent(exam, student, exam.X, exam.Y);

                var entry = archive.CreateEntry($"{student.FullName}.pdf");

                using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);
            }
        }

        var zipBytes = zipStream.ToArray();

        return Result.Success(new FileExamResponse(
            File: zipBytes,
            FileName: $"{exam.Title}.zip",
            ContentType: "application/zip"
        ));
    }

    private int GetPdfPageCount(string path)
    {
        using var pdf = new PdfDocument(new PdfReader(path));
        return pdf.GetNumberOfPages();
    }

    private async Task<byte[]> GenerateForStudent(Exam exam, Student student, double x, double y)
    {
        // مسار الـ PDF الأصلي للامتحان
        var templatePath = Path.Combine(_webHostEnvironment.WebRootPath, exam.PdfPath);

        // لو الطالب ما عندوش ملف PDF، نعتبره صورة → نحتاج نولد PDF مؤقت
        byte[] pdfBytes;

        // أولاً نتحقق إذا الطالب عنده ExamPaper سابق
        var studentPaper = await _context.StudentExamPapers
            .Include(p => p.Pages)
            .FirstOrDefaultAsync(p => p.ExamId == exam.Id && p.StudentId == student.Id);

        if (studentPaper is null)
        {
            studentPaper = new StudentExamPaper
            {
                ExamId = exam.Id,
                StudentId = student.Id,
                GeneratedAt = DateTime.Now,
                Pages = []
            };
            _context.StudentExamPapers.Add(studentPaper);
            await _context.SaveChangesAsync();
        }
        else
        {
            _context.StudentExamPages.RemoveRange(studentPaper.Pages);
            studentPaper.Pages.Clear();
            await _context.SaveChangesAsync();
        }

        // الآن نقرأ ملف الامتحان الأصلي (PDF)
        using (var reader = new PdfReader(templatePath))
        using (var memoryStream = new MemoryStream())
        using (var writer = new PdfWriter(memoryStream))
        using (var pdf = new PdfDocument(reader, writer))
        {
            var doc = new Document(pdf);
            int totalPages = pdf.GetNumberOfPages();

            for (int page = 1; page <= totalPages; page++)
            {
                // توليد الباركود
                var barcodeValue = $"{exam.Id}-{student.Id}-{page}";
                var barcode = new Barcode128(pdf);
                barcode.SetCode(barcodeValue);
                barcode.SetX(1.0f);
                barcode.SetBarHeight(35f);
                barcode.SetBaseline(10f);
                barcode.SetSize(10f);

                var barcodeImage = new Image(barcode.CreateFormXObject(pdf))
                    .Scale(1.3f, 1.3f)
                    .SetFixedPosition(page, (float)x, (float)y);

                doc.Add(barcodeImage);

                // حفظ بيانات الصفحة في الـ DB
                studentPaper.Pages.Add(new StudentExamPage
                {
                    PageNumber = page,
                    BarcodeValue = barcodeValue
                });
            }

            await _context.SaveChangesAsync();
            doc.Close();

            pdfBytes = memoryStream.ToArray();
        }

        return pdfBytes;
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
   
    
    public async Task<Result<TeacherExamResponse>> UploadTeacherExamAsync(
        UploadTeacherExamRequest request)
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
            _webHostEnvironment.ContentRootPath, "Uploads");

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
            teacherExam.PdfPath,
            teacherExam.QuestionsJson
        ));
    }

   

}

