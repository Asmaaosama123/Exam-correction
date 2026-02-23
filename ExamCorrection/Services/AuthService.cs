using System.Security.Cryptography;

namespace ExamCorrection.Services;

public class AuthService(UserManager<ApplicationUser> userManager, IJwtProvider jwtProvider,
    SignInManager<ApplicationUser> signInManager) : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IJwtProvider _jwtProvider = jwtProvider;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly int _refreshTokenExpiryDays = 14;

    public async Task<Result<AuthResponse>> GetTokenAsync(string identifier, string password, bool isEmail, CancellationToken cancellationToken = default)
    {
        ApplicationUser? user;

        if (isEmail)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return Result.Failure<AuthResponse>(UserErrors.InvalidCredentials);

            user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == identifier, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return Result.Failure<AuthResponse>(UserErrors.InvalidCredentials);

            user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == identifier, cancellationToken);
        }

        if (user is null)
            return Result.Failure<AuthResponse>(UserErrors.InvalidCredentials);

        if (user.IsDisabled)
            return Result.Failure<AuthResponse>(UserErrors.DisabledUser);

        var result = await _signInManager.PasswordSignInAsync(user, password, false, true);

        if (result.Succeeded)
        {
            var userRoles = await _userManager.GetRolesAsync(user);

            var (token, expiresIn) = _jwtProvider.GenerateToken(user, userRoles);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays);

            user.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                ExpiresOn = refreshTokenExpiration
            });

            await _userManager.UpdateAsync(user);

            var response = new AuthResponse(user.Id, user.FirstName, user.LastName, token, expiresIn, refreshToken, refreshTokenExpiration);

            return Result.Success(response);
        }

        var error = result.IsNotAllowed
            ? UserErrors.EmailNotConfirmed
            : result.IsLockedOut
            ? UserErrors.LockedUser
            : UserErrors.InvalidCredentials;

        return Result.Failure<AuthResponse>(error);
    }

    public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (request.IsEmail)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return Result.Failure(UserErrors.EmptyEmail);

            var emailExists = await _userManager.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);

            if (emailExists)
                return Result.Failure(UserErrors.DuplicatedEmail);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return Result.Failure(UserErrors.EmptyPhoneNumber);

            var phoneExists = await _userManager.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber, cancellationToken);

            if (phoneExists)
                return Result.Failure(UserErrors.DuplicatedPhonenumber);
        }

        ApplicationUser user = new()
        {
            Email = request.IsEmail ? request.Email : null,
            UserName = request.IsEmail ? request.Email : request.PhoneNumber,
            PhoneNumber = !request.IsEmail ? request.PhoneNumber : null,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            //var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            //code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            //_logger.LogInformation(message: "Confirmation code: {code}", code);

            //await SendConfirmationEmail(user, code);

            return Result.Success();
        }

        var error = result.Errors.First();

        return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    }

    //public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request)
    //{
    //    if (await _userManager.FindByIdAsync(request.UserId) is not { } user)
    //        return Result.Failure(UserErrors.InvalidCode);

    //    if (user.EmailConfirmed)
    //        return Result.Failure(UserErrors.DuplicatedConfirmation);

    //    var code = request.Code;

    //    try
    //    {
    //        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
    //    }
    //    catch (FormatException)
    //    {
    //        return Result.Failure(UserErrors.InvalidCode);
    //    }

    //    var result = await _userManager.ConfirmEmailAsync(user, code);

    //    if (result.Succeeded)
    //    {
    //        await _userManager.AddToRoleAsync(user, DefaultRoles.Member);
    //        return Result.Success();
    //    }

    //    var error = result.Errors.First();

    //    return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
    //}

    //public async Task<Result> ResendConfirmationEmailAsync(ResendConfirmationEmailRequest request)
    //{
    //    if (await _userManager.FindByEmailAsync(request.Email) is not { } user)
    //        return Result.Success();

    //    if (user.EmailConfirmed)
    //        return Result.Failure(UserErrors.DuplicatedConfirmation);

    //    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    //    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

    //    _logger.LogInformation(message: "Confirmation code: {code}", code);

    //    await SendConfirmationEmail(user, code);

    //    return Result.Success();
    //}

    public async Task<Result<AuthResponse>> GetRefreshTokenAsync(string token, string refreshToken, CancellationToken cancellationToken = default)
    {
        var userId = _jwtProvider.ValidateToken(token);

        if (userId is null)
            return Result.Failure<AuthResponse>(UserErrors.InvalidJwtToken);

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
            return Result.Failure<AuthResponse>(UserErrors.InvalidCredentials);

        if (user.IsDisabled)
            return Result.Failure<AuthResponse>(UserErrors.DisabledUser);

        if (user.LockoutEnd > DateTime.UtcNow)
            return Result.Failure<AuthResponse>(UserErrors.LockedUser);

        var userRefreshToken = user.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken && x.IsActive);

        if (userRefreshToken is null)
            return Result.Failure<AuthResponse>(UserErrors.InvalidRefreshToken);

        userRefreshToken.RevokedOn = DateTime.UtcNow;

        var userRoles = await _userManager.GetRolesAsync(user);

        var (newToken, expiresIn) = _jwtProvider.GenerateToken(user, userRoles);
        var newRefreshToken = GenerateRefreshToken();
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays);

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = newRefreshToken,
            ExpiresOn = refreshTokenExpiration
        });

        await _userManager.UpdateAsync(user);

        var response = new AuthResponse(user.Id, user.FirstName, user.LastName, newToken, expiresIn, newRefreshToken, refreshTokenExpiration);

        return Result.Success(response);
    }

    //public async Task<Result> SendResetPasswordCodeAsync(string email)
    //{
    //    if (await _userManager.FindByEmailAsync(email) is not { } user)
    //        return Result.Success();

    //    if (!user.EmailConfirmed)
    //        return Result.Failure(UserErrors.EmailNotConfirmed with { StatusCode = StatusCodes.Status400BadRequest });

    //    var code = await _userManager.GeneratePasswordResetTokenAsync(user);
    //    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

    //    _logger.LogInformation(message: "Confirmation code: {code}", code);

    //    await SendResetPasswordEmail(user, code);

    //    return Result.Success();
    //}

    //public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    //{
    //    var user = await _userManager.FindByEmailAsync(request.Email);

    //    if (user is null || !user.EmailConfirmed)
    //        return Result.Failure(UserErrors.InvalidCode);

    //    IdentityResult result;

    //    try
    //    {
    //        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
    //        result = await _userManager.ResetPasswordAsync(user, code, request.NewPassword);
    //    }
    //    catch (FormatException)
    //    {
    //        result = IdentityResult.Failed(_userManager.ErrorDescriber.InvalidToken());
    //    }

    //    if (result.Succeeded)
    //        return Result.Success();

    //    var error = result.Errors.First();

    //    return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status401Unauthorized));
    //}

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    //private async Task SendConfirmationEmail(ApplicationUser user, string code)
    //{
    //    var origin = _httpContextAccessor.HttpContext?.Request.Headers.Origin;

    //    var placeholders = new Dictionary<string, string>
    //    {
    //        { "{{name}}", user.FirstName },
    //        { "{{action_url}}", $"{origin}/auth/emailConfirmation?userId={user.Id}&code={code}" }
    //    };

    //    var emailBody = EmailBodyBuilder.GenerateEmailBody("EmailConfirmation", placeholders);

    //    BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(user.Email!, "✅ Exam Correction: Email Confirmation", emailBody));

    //    await Task.CompletedTask;
    //}

    //private async Task SendResetPasswordEmail(ApplicationUser user, string code)
    //{
    //    var origin = _httpContextAccessor.HttpContext?.Request.Headers.Origin;

    //    var placeholders = new Dictionary<string, string>
    //    {
    //        { "{{name}}", user.FirstName },
    //        { "{{action_url}}", $"{origin}/auth/forgetPassword?userId={user.Email}&code={code}" }
    //    };

    //    var emailBody = EmailBodyBuilder.GenerateEmailBody("ForgetPassword", placeholders);

    //    BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(user.Email!, "✅ Exam Correction: Change Password", emailBody));

    //    await Task.CompletedTask;
    //}
}