namespace ExamCorrection.Abstractions.Consts;

public static class ErrorMessages
{
	public const string WeakPassword = "passwords must contain an uppercase character, lowercase character, a digit, and a non-alphanumeric character. Passwords must be at least 8 characters long.";
	public const string DuplicatedRole = "you cannot add duplicated role for the same user";
}