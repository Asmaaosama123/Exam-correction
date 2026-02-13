namespace ExamCorrection.Authentication;

public class JwtProvider(IOptions<JwtOptions> jwtOptions) : IJwtProvider
{
	private readonly JwtOptions _jwtOptions = jwtOptions.Value;

	public (string token, int expiresIn) GenerateToken(ApplicationUser user, IEnumerable<string> roles)
	{
		Claim[] claims = [
			new(JwtRegisteredClaimNames.Sub, user.Id),
			new(JwtRegisteredClaimNames.GivenName, user.FirstName),
			new(JwtRegisteredClaimNames.FamilyName, user.LastName),
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new(nameof(roles), JsonSerializer.Serialize(roles), JsonClaimValueTypes.JsonArray),
		];

		var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));

		var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

		var token = new JwtSecurityToken(
			issuer: _jwtOptions.Issuer,
			audience: _jwtOptions.Audience,
			claims: claims,
			expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
			signingCredentials: signingCredentials
		);

		return (token: new JwtSecurityTokenHandler().WriteToken(token), expiresIn: _jwtOptions.ExpiryMinutes * 120);
	}

	public string? ValidateToken(string token)
	{
		var tokenHnadler = new JwtSecurityTokenHandler();
		var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
		try
		{
			tokenHnadler.ValidateToken(token, new TokenValidationParameters
			{
				IssuerSigningKey = symmetricSecurityKey,
				ValidateIssuerSigningKey = true,
				ValidateIssuer = false,
				ValidateAudience = false,
				ClockSkew = TimeSpan.Zero
			}, out SecurityToken validatedToken);

			var jwtTokern = (JwtSecurityToken)validatedToken;

			return jwtTokern.Claims.First(x => x.Type == JwtRegisteredClaimNames.Sub).Value;
		}
		catch
		{
			return null;
		}

	}
}