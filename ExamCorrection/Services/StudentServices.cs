using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using ExamCorrection.Contracts;
using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Dynamic.Core;
using System.Text.RegularExpressions;

namespace ExamCorrection.Services;

public class StudentServices(ApplicationDbContext _context, StudentRequestValidator validator) : IStudentServices
{
    private readonly ApplicationDbContext _context = _context;

    private readonly List<string> _allowdExtensions = [".xlsx"];
    private readonly int _maxAllowedSize = 7340032;

    public async Task<Result<PaginatedList<StudentResponse>>> GetAllAsync(int? classId, RequestFilters filters, CancellationToken cancellationToken = default)
    {
        var students = _context.Students.AsQueryable();

        if (classId is not null)
            students = students.Where(x => x.ClassId == classId);

        if (!string.IsNullOrEmpty(filters.SearchValue))
            students = students.Where(x => x.FullName.Contains(filters.SearchValue, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(filters.SortColumn))
            students = students.OrderBy($"{filters.SortColumn} {filters.SortDirection}");

        var source = students.ProjectToType<StudentResponse>();

        var response = await PaginatedList<StudentResponse>.CreateAsync(source, filters.PageNumber, filters.PageSize, cancellationToken);

        return Result.Success(response);
    }

    public async Task<Result<StudentResponse>> GetAsync(int? classId, int studentId, CancellationToken cancellationToken = default)
    {
        var query = _context.Students.AsQueryable();

        if (classId is not null)
            query = query.Where(x => x.ClassId == classId);

        var student = await _context.Students
            .Where(x => x.Id == studentId)
            .ProjectToType<StudentResponse>()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (student is null)
            return Result.Failure<StudentResponse>(StudentErrors.StudentNotFound);

        return Result.Success(student);
    }

    public async Task<Result<StudentResponse>> AddAsync(int classId, StudentRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _context.Classes.AnyAsync(x => x.Id == classId, cancellationToken))
            return Result.Failure<StudentResponse>(ClassErrors.ClassNotFound);

        if (!string.IsNullOrEmpty(request.MobileNumber))
        {
            var isExistsMobileNumber = await _context.Students.AnyAsync(x => x.MobileNumber == request.MobileNumber, cancellationToken);

            if (isExistsMobileNumber)
                return Result.Failure<StudentResponse>(StudentErrors.DuplicatedMobileNumber);
        }
        if (!string.IsNullOrEmpty(request.Email))
        {
            var isExistsEmail = await _context.Students.AnyAsync(x => x.Email == request.Email, cancellationToken);

            if (isExistsEmail)
                return Result.Failure<StudentResponse>(StudentErrors.DuplicatedEmail);
        }

        var student = request.Adapt<Student>();
        student.ClassId = classId;

        await _context.Students.AddAsync(student, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var classEntity = await _context.Classes.FindAsync(classId, cancellationToken);

        var response = student.Adapt<StudentResponse>() with
        {
            ClassName = classEntity is { } ? classEntity.Name : string.Empty
        };

        return Result.Success(response);
    }

    public async Task<Result> UpdateAsync(int studentId, UpdateStudentRequest request, CancellationToken cancellationToken = default)
    {
        if (await _context.Students.FindAsync(studentId) is not { } student)
            return Result.Failure<StudentResponse>(StudentErrors.StudentNotFound);

        if (!string.IsNullOrEmpty(request.MobileNumber))
        {
            var isExistsMobileNumber = await _context.Students.AnyAsync(x => x.MobileNumber == request.MobileNumber && x.Id != studentId, cancellationToken);

            if (isExistsMobileNumber)
                return Result.Failure<StudentResponse>(StudentErrors.DuplicatedMobileNumber);
        }
        if (!string.IsNullOrEmpty(request.Email))
        {
            var isExistsEmail = await _context.Students.AnyAsync(x => x.Email == request.Email && x.Id != studentId, cancellationToken);

            if (isExistsEmail)
                return Result.Failure<StudentResponse>(StudentErrors.DuplicatedEmail);
        }

        request.Adapt(student);
        student.ClassId = request.ClassId; 
        student.IsDisabled = request.IsDisabled;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> Delete(int studentId, CancellationToken cancellationToken = default)
    {
        if (await _context.Students.FindAsync(studentId) is not { } student)
            return Result.Failure(StudentErrors.StudentNotFound);

        _context.Students.Remove(student);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<BulkImportFileResponse>> ImportStudentsAsync(IFormFile file, int classId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (file.Length > _maxAllowedSize)
                return Result.Failure<BulkImportFileResponse>(FileErrors.MaxFileSize);

            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!_allowdExtensions.Contains(extension))
                return Result.Failure<BulkImportFileResponse>(FileErrors.NotAllowedExtension);

            if (!await _context.Classes.AnyAsync(x => x.Id == classId, cancellationToken))
                return Result.Failure<BulkImportFileResponse>(ClassErrors.ClassNotFound);

            var duplicateSets = await PrepareDuplicateSetsAsync(cancellationToken);

            var students = new List<Student>();
            int affectedRows = 0;
            int failedRows = 0;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var headers = PrepareArabicHeaders(worksheet, out int headerRowNumber);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRowNumber))
            {
                var fullName = GetCell(row, headers, "الاسم");
                var nationalId = GetCell(row, headers, "رقم السجل المدني");

                if (!TryBuildStudent(fullName, nationalId, classId, duplicateSets, out var student))
                {
                    failedRows++;
                    continue;
                }

                students.Add(student);
                affectedRows++;
            }
            
            if (students.Any())
            {
                await _context.Students.AddRangeAsync(students, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new BulkImportFileResponse(affectedRows, failedRows));
        }
        catch
        {
            return Result.Failure<BulkImportFileResponse>(FileErrors.FileNotFound);
        }
    }

    private async Task<DuplicateCheckSets> PrepareDuplicateSetsAsync(CancellationToken cancellationToken)
    {
        var existingNationalIds = await _context.Students
            .Select(x => x.NationalId!)
            .ToListAsync(cancellationToken);

        return new DuplicateCheckSets
        {
            FileNationalIds = [],
            NationalIdSet = new HashSet<string>(existingNationalIds)
        };
    }

    private Dictionary<string, int> PrepareArabicHeaders(IXLWorksheet worksheet, out int headerRowNumber)
    {
        foreach (var row in worksheet.RowsUsed())
        {
            foreach (var cell in row.CellsUsed())
            {
                var text = (cell.IsMerged()
                    ? cell.MergedRange().FirstCell().GetString()
                    : cell.GetString())
                    .Replace("ـ", "")
                    .Trim()
                    .ToLower();

                if (text.Contains("الاسم", StringComparison.OrdinalIgnoreCase))
                {
                    headerRowNumber = row.RowNumber();

                    var headers = new Dictionary<string, int>();

                    headers["الاسم"] = cell.Address.ColumnNumber;

                    var nidCell = row.CellsUsed()
                        .FirstOrDefault(c =>
                            (c.IsMerged()
                                ? c.MergedRange().FirstCell().GetString()
                                : c.GetString()).Replace("ـ", "")
                            .Trim()
                            .ToLower()
                            .Contains("رقم", StringComparison.OrdinalIgnoreCase) &&
                            (c.IsMerged()
                                ? c.MergedRange().FirstCell().GetString()
                                : c.GetString())
                            .ToLower()
                            .Contains("السجل", StringComparison.OrdinalIgnoreCase));

                    if (nidCell is null)
                        throw new Exception("Invalid Template");

                    headers["رقم السجل المدني"] = nidCell.Address.ColumnNumber;

                    return headers;
                }
            }
        }

        throw new Exception("Invalid Template");
    }

    private string GetCell(IXLRow row,Dictionary<string, int> headers, string headerName)
    {
        headerName = headerName.Replace("ـ", "").Trim().ToLower();

        return headers.TryGetValue(headerName, out var index)
            ? row.Cell(index).GetString().Trim()
            : string.Empty;
    }

    private bool TryBuildStudent(string fullName, string nationalId, int classId, DuplicateCheckSets sets, out Student student)
    {
        student = null!;

        fullName = fullName.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            return false;

        if (string.IsNullOrWhiteSpace(nationalId))
            return false;

        if (sets.NationalIdSet!.Contains(nationalId))
            return false;

        if (!sets.FileNationalIds!.Add(nationalId))
            return false;

        var request = new StudentRequest(
            FullName: fullName,
            NationalId: nationalId,
            Email: null,
            MobileNumber: null
        );

        student = request.Adapt<Student>();
        student.ClassId = classId;

        return true;
    }
}