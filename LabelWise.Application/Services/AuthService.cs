using System;
using System.Threading.Tasks;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _users;
        private readonly IPasswordHasher _hasher;
        private readonly IJwtTokenService _jwt;

        public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt)
        {
            _users = users;
            _hasher = hasher;
            _jwt = jwt;
        }

        public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            // validation
            if (string.IsNullOrWhiteSpace(request.Email)) throw new ArgumentException("Email is required.");
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6) throw new ArgumentException("Password must be at least 6 characters.");


            var existing = await _users.GetByEmailAsync(request.Email);
            if (existing != null) throw new InvalidOperationException("Email already in use.");

            var (hash, salt) = _hasher.HashPassword(request.Password);

            var user = new User(request.Email, hash, salt);
            var profile = new LabelWise.Domain.Entities.UserProfile(user.Id, LabelWise.Domain.Enums.GoalType.Undefined);
            user.SetProfile(profile);

            await _users.AddAsync(user);
            await _users.SaveChangesAsync();

            var token = _jwt.GenerateToken(user);

            return new RegisterResponseDto
            {
                Id = user.Id.ToString(),
                Email = user.Email,
                Token = token
            };
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)) throw new ArgumentException("Invalid credentials.");

            var user = await _users.GetByEmailAsync(request.Email);
            if (user == null) throw new InvalidOperationException("Invalid credentials.");

            if (!_hasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt)) throw new InvalidOperationException("Invalid credentials.");

            var token = _jwt.GenerateToken(user);

            return new LoginResponseDto
            {
                Token = token,
                Email = user.Email,
                Id = user.Id.ToString()
            };
        }
    }
}
