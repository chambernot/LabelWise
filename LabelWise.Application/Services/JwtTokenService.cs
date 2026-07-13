using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using LabelWise.Shared.Options;
using LabelWise.Domain.Entities;
using LabelWise.Application.Interfaces;

namespace LabelWise.Application.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _settings;
        private readonly byte[] _keyBytes;

        public JwtTokenService(IOptions<JwtSettings> options)
        {
            _settings = options.Value;
            _keyBytes = Encoding.UTF8.GetBytes(_settings.Key);
        }

        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(_keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_settings.TokenExpiryMinutes),
                signingCredentials: creds
            );

            return tokenHandler.WriteToken(token);
        }
    }
}
