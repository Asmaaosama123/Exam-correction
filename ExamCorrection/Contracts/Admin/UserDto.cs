namespace ExamCorrection.Contracts.Admin;

public record UserDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    bool IsDisabled
);
