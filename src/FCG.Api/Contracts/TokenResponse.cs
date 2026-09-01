namespace FCG.Api.Contracts;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
