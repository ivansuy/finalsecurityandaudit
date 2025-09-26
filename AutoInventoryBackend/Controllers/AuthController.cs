using AutoInventoryBackend.DTOs;
using AutoInventoryBackend.Models;
using AutoInventoryBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AutoInventoryBackend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IJwtTokenService _jwt;
        private readonly ILoginBackoffService _backoff;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            SignInManager<ApplicationUser> signIn,
            UserManager<ApplicationUser> users,
            IJwtTokenService jwt,
            ILoginBackoffService backoff,
            ILogger<AuthController> logger)
        {
            _signIn = signIn; _users = users; _jwt = jwt; _backoff = backoff; _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var user = await _users.FindByEmailAsync(dto.Email);
            var backoffKey = ILoginBackoffService.BuildKey(ip, dto.Email);

            if (user == null)
            {
                await _backoff.RegisterAttemptAsync(backoffKey, false, ip, dto.Email, "UserNotFound");
                return Unauthorized(new LoginResponseDto { Message = "Credenciales inválidas" });
            }

            var valid = await _users.CheckPasswordAsync(user, dto.Password);
            if (!valid)
            {
                var (delay, _, blocked) = await _backoff.RegisterAttemptAsync(backoffKey, false, ip, dto.Email, "InvalidPassword");
                if (blocked) return Unauthorized(new LoginResponseDto { Message = "Bloqueado temporalmente" });
                await Task.Delay(delay);
                return Unauthorized(new LoginResponseDto { Message = "Credenciales inválidas" });
            }

            if (user.TwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(dto.OtpCode))
                {
                    await _backoff.RegisterAttemptAsync(backoffKey, false, ip, dto.Email, "MfaRequired");
                    return Ok(new LoginResponseDto { RequiresMfa = true, Message = "MFA requerido" });
                }

                var valid2fa = await _users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, dto.OtpCode);
                if (!valid2fa)
                {
                    var (delay, _, blocked) = await _backoff.RegisterAttemptAsync(backoffKey, false, ip, dto.Email, "InvalidOtp");
                    if (blocked) return Unauthorized(new LoginResponseDto { Message = "Bloqueado temporalmente" });
                    await Task.Delay(delay);
                    return Unauthorized(new LoginResponseDto { Message = "Código MFA inválido" });
                }
            }

            await _backoff.RegisterAttemptAsync(backoffKey, true, ip, dto.Email, "LoginOk");

            var roles = await _users.GetRolesAsync(user);
            var (token, exp) = _jwt.CreateToken(user, roles);
            return Ok(new LoginResponseDto { RequiresMfa = false, Token = token, ExpiresAtUtc = exp, Message = "OK" });
        }

        [HttpPost("enable-mfa")]
        [Authorize]
        public async Task<ActionResult<EnableMfaResponseDto>> EnableMfa()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Si ya tiene MFA habilitado, no resetear clave silenciosamente
            if (user.TwoFactorEnabled)
            {
                return BadRequest("MFA ya habilitado. Usa tu app actual. Si necesitas regenerar la clave, deshabilítalo primero.");
            }

            // Si no hay clave, la genero; evita regenerarla cada vez que se visita la pantalla
            var key = await _users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await _users.ResetAuthenticatorKeyAsync(user);
                key = await _users.GetAuthenticatorKeyAsync(user) ?? "";
            }

            // Normalizar para compatibilidad con Google/Microsoft Authenticator
            key = key.Replace(" ", "").ToUpperInvariant();

            var email = user.Email ?? user.UserName!;
            var issuer = "AutoInventory";
            var otpauth = $"otpauth://totp/{WebUtility.UrlEncode(issuer)}:{WebUtility.UrlEncode(email)}" +
                          $"?secret={key}&issuer={WebUtility.UrlEncode(issuer)}&digits=6";

            return Ok(new EnableMfaResponseDto { ManualKey = key, OtpauthUri = otpauth });
        }


        [HttpPost("verify-mfa")]
        [Authorize]
        public async Task<IActionResult> VerifyMfa([FromBody] string code)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var ok = await _users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);
            if (!ok) return BadRequest("Código inválido");

            // Mejor usar el helper de Identity
            var result = await _users.SetTwoFactorEnabledAsync(user, true);
            if (!result.Succeeded) return StatusCode(500, "No se pudo habilitar MFA.");

            return Ok("MFA habilitado");
        }

    }
}
