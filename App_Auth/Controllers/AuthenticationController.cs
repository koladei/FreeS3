using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CradleSoft.DMS.Data;
using CradleSoft.DMS.Models;
using CradleSoft.DMS.Models.Dtos;
using CradleSoft.DMS.Models.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace App_Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string AccessTokenCookieName = "access_token";
    private const string RefreshTokenCookieName = "refresh_token";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IConfiguration configuration,
        TokenValidationParameters tokenValidationParameters)
    {
        _userManager = userManager;
        _context = context;
        _configuration = configuration;
        _tokenValidationParameters = tokenValidationParameters;
    }

    [HttpPost("register-user")]
    public async Task<IActionResult> Register([FromBody] RegisterUserDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = ApplicationUserMapper.RegisterUserDtoToApplicationUser(model);
        var userExists = await _userManager.FindByNameAsync(model.Username);
        if (userExists != null)
        {
            return BadRequest(new { Message = "User already exists!" });
        }

        user.SecurityStamp = Guid.NewGuid().ToString();
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return Ok();
    }

    [Authorize]
    [HttpGet("session")]
    public IActionResult Session()
    {
        return Ok(new
        {
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
            Username = User.Identity?.Name,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
        {
            return Unauthorized(new { Message = "The username and/or password is incorrect" });
        }

        var token = await GenerateJwtToken(user);
        WriteAuthCookies(token);
        return Ok(new { Message = "Login successful", ExpiresAt = token.ExpiresAt });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto? model)
    {
        var requestModel = model ?? new RefreshTokenDto();
        requestModel.Token = string.IsNullOrWhiteSpace(requestModel.Token)
            ? Request.Cookies[AccessTokenCookieName] ?? string.Empty
            : requestModel.Token;
        requestModel.RefreshToken = string.IsNullOrWhiteSpace(requestModel.RefreshToken)
            ? Request.Cookies[RefreshTokenCookieName] ?? string.Empty
            : requestModel.RefreshToken;

        if (string.IsNullOrWhiteSpace(requestModel.Token) || string.IsNullOrWhiteSpace(requestModel.RefreshToken))
        {
            return BadRequest(new { Message = "Missing access or refresh token." });
        }

        try
        {
            var result = await VerifyAndGenerateToken(requestModel);
            WriteAuthCookies(result);
            return Ok(new { Message = "Token refreshed", ExpiresAt = result.ExpiresAt });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        ClearAuthCookies();
        return Ok(new { Message = "Logged out" });
    }

    private async Task<AuthResultDto> VerifyAndGenerateToken(RefreshTokenDto refreshModel)
    {
        var jwtTokenHandler = new JwtSecurityTokenHandler();
        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshModel.RefreshToken);
        if (storedToken == null)
        {
            throw new SecurityTokenException("Invalid refresh token");
        }

        if (storedToken.JwtId != jwtTokenHandler.ReadJwtToken(refreshModel.Token).Id)
        {
            throw new SecurityTokenException("Refresh token does not match the provided JWT");
        }

        if (storedToken.Expires < DateTime.UtcNow || !storedToken.IsActive)
        {
            throw new SecurityTokenException("Refresh token is no longer valid");
        }

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            throw new SecurityTokenException("User associated with refresh token was not found");
        }

        try
        {
            jwtTokenHandler.ValidateToken(refreshModel.Token, _tokenValidationParameters, out _);
            return await GenerateJwtToken(user, storedToken);
        }
        catch (SecurityTokenExpiredException)
        {
            return storedToken.Expires >= DateTime.UtcNow
                ? await GenerateJwtToken(user, storedToken)
                : await GenerateJwtToken(user);
        }
    }

    private async Task<AuthResultDto> GenerateJwtToken(ApplicationUser user, RefreshToken? refreshToken = null)
    {
        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var secretKey = _configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is missing from configuration.");
        var issuer = _configuration["JwtSettings:Issuer"];
        var audience = _configuration["JwtSettings:Audience"];
        var expirationMinutes = _configuration.GetValue<int?>("JwtSettings:ExpirationInMinutes") ?? 60;
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );
        var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

        if (refreshToken == null)
        {
            refreshToken = new RefreshToken
            {
                JwtId = token.Id,
                Token = $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
                CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserId = user.Id
            };
            await _context.RefreshTokens.AddAsync(refreshToken);
        }

        refreshToken.JwtId = token.Id;
        await _context.SaveChangesAsync();

        return new AuthResultDto
        {
            Token = jwtToken,
            ExpiresAt = token.ValidTo,
            RefreshToken = refreshToken.Token
        };
    }

    private void WriteAuthCookies(AuthResultDto authResult)
    {
        var accessTokenExpires = authResult.ExpiresAt <= DateTime.UtcNow
            ? DateTime.UtcNow.AddMinutes(60)
            : authResult.ExpiresAt;

        Response.Cookies.Append(AccessTokenCookieName, authResult.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = accessTokenExpires,
            Path = "/"
        });

        Response.Cookies.Append(RefreshTokenCookieName, authResult.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7),
            Path = "/"
        });
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }
}
