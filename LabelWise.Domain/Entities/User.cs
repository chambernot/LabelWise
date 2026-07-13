using System;
using System.Collections.Generic;
using LabelWise.Domain.Common;

namespace LabelWise.Domain.Entities
{
    public class User : AuditableEntity
    {
        // Authentication
        public string Email { get; private set; }
        public string PasswordHash { get; private set; }
        public string? PasswordSalt { get; private set; }

        // Relations
        public virtual UserProfile? Profile { get; private set; }

        // Security / account
        public bool IsEmailConfirmed { get; private set; }
        public bool IsLocked { get; private set; }

        // Navigation for analyses done by the user
        public virtual ICollection<ProductAnalysis> Analyses { get; private set; } = new List<ProductAnalysis>();

        protected User() { }

        public User(string email, string passwordHash, string? passwordSalt = null)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
            PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
            PasswordSalt = passwordSalt;
        }

        public void ConfirmEmail()
        {
            IsEmailConfirmed = true;
            SetUpdated();
        }

        public void Lock()
        {
            IsLocked = true;
            SetUpdated();
        }

        public void Unlock()
        {
            IsLocked = false;
            SetUpdated();
        }

        public void UpdatePassword(string newHash, string? newSalt = null)
        {
            PasswordHash = newHash ?? throw new ArgumentNullException(nameof(newHash));
            PasswordSalt = newSalt;
            SetUpdated();
        }

        public void SetProfile(UserProfile profile)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            SetUpdated();
        }
    }
}
