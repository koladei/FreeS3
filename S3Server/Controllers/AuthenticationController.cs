using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CradleSoft.DMS.Data;
using CradleSoft.DMS.Models;
using CradleSoft.DMS.Models.Mappers;
using CradleSoft.DMS.Models.Dtos;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace S3Server.Controllers;

[ApiController]
// [Route("S3Server/[controller]")]
[Route("sch")]
public class AuthenticationController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    private readonly TokenValidationParameters _tokenValidationParameters;

    public AuthenticationController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context, IConfiguration configuration, TokenValidationParameters tokenValidationParameters)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _configuration = configuration;
        _tokenValidationParameters = tokenValidationParameters;
    }

    [HttpPost("register-user")]
    public async Task<IActionResult> Register([FromBody] RegisterUserDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        ApplicationUser user = ApplicationUserMapper.RegisterUserDtoToApplicationUser(model);

        var userExists = await _userManager.FindByNameAsync(user.UserName);
        if (userExists != null)
            return BadRequest(new { Message = "User already exists!" });

        user.SecurityStamp = Guid.NewGuid().ToString();
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("session")]
    public async Task<IActionResult> Session()
    {
        return Ok(new { Message = "Session is active" });
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            return Unauthorized(new { Message = "The username and/or password is incorrect" });

        var token = await GenerateJwtToken(user);

        return Ok(token);
    }


    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await VerifyAndGenerateToken(model);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
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
            if (storedToken.Expires >= DateTime.UtcNow)
            {
                return await GenerateJwtToken(user, storedToken);
            }
            else
            {
                return await GenerateJwtToken(user);
            }
        }
    }

    private async Task<AuthResultDto> GenerateJwtToken(ApplicationUser user, RefreshToken? refreshToken = null)
    {
        var authClaims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Sub, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var secretKey = _configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is missing from configuration.");
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:ValidIssuer"],
            audience: _configuration["JwtSettings:ValidAudience"],
            expires: DateTime.UtcNow.AddMinutes(1),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );
        var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

        if (refreshToken == null)
        {
            refreshToken = new RefreshToken
            {
                JwtId = token.Id,
                Token = Guid.NewGuid().ToString() + Guid.NewGuid().ToString(),
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
                CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserId = user.Id
            };
            await _context.RefreshTokens.AddAsync(refreshToken);
        }
        refreshToken.JwtId = token.Id;
        await _context.SaveChangesAsync();

        // Implementation for generating JwtSettings token goes here
        return new AuthResultDto
        {
            Token = jwtToken,
            ExpiresAt = token.ValidTo,
            RefreshToken = refreshToken.Token
        };
    }
}
