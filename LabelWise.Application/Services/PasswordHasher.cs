using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using LabelWise.Application.Interfaces;

namespace LabelWise.Application.Services
{
    public class PasswordHasher : IPasswordHasher
    {
        // Uses PBKDF2 via Microsoft.AspNetCore.Cryptography.KeyDerivation
        public (string hash, string? salt) HashPassword(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            string saltBase64 = Convert.ToBase64String(salt);
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100_000,
                numBytesRequested: 256 / 8));

            return (hashed, saltBase64);
        }

        public bool Verify(string password, string hash, string? salt)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (hash == null) throw new ArgumentNullException(nameof(hash));

            if (salt == null) return false;

            byte[] saltBytes = Convert.FromBase64String(salt);
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: saltBytes,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100_000,
                numBytesRequested: 256 / 8));

            return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hashed), Convert.FromBase64String(hash));
        }
    }
}
