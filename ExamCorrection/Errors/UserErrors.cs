namespace ExamCorrection.Errors;

public record UserErrors
{
	public static readonly Error InvalidCredentials =
		new("User.InvalidCredentials", "رقم الهاتف أو كلمة المرور غير صالحة", StatusCodes.Status401Unauthorized);

	public static readonly Error DisabledUser =
		new("User.DisabledUser", "Disabled user, please contact with administrator", StatusCodes.Status401Unauthorized);

	public static readonly Error LockedUser =
		new("User.LockedUser", "Locked user, please contact with administrator", StatusCodes.Status401Unauthorized);

	public static readonly Error InvalidJwtToken =
		new("User.InvalidJwtToken", "Invalid Jwt token", StatusCodes.Status401Unauthorized);

	public static readonly Error InvalidRefreshToken =
		new("User.InvalidRefreshToken", "Invalid refresh token", StatusCodes.Status401Unauthorized);

    public static readonly Error DuplicatedPhonenumber =
        new("User.DuplicaetdPhonenumber", "رقم الهاتف موجود بالفعل", StatusCodes.Status409Conflict);

	public static readonly Error DuplicatedEmail =
		new("User.DuplicaetdPhonenumber", "البريد الإلكتروني موجود بالفعل", StatusCodes.Status409Conflict);

    public static readonly Error EmptyPhoneNumber =
    new("User.EmptyPhoneNumber", "رقم الهاتف مطلوب", StatusCodes.Status400BadRequest);

    public static readonly Error EmptyEmail =
        new("User.EmptyPhoneNumber", "البريد الإلكتروني مطلوب", StatusCodes.Status400BadRequest);

    public static readonly Error EmailNotConfirmed =
		new("User.EmailNotConfirmed", "Email is not confirmed", StatusCodes.Status401Unauthorized);
}