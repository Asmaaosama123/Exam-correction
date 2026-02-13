namespace ExamCorrection.Contracts.Profile;

public record CurrentUserResponse(
    string Id,
    string FirstName,
    string LastName
);