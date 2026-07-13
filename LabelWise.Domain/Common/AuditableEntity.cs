using System;

namespace LabelWise.Domain.Common
{
    public abstract class AuditableEntity
    {
        public Guid Id { get; protected set; }

        public DateTimeOffset CreatedAt { get; protected set; }
        public DateTimeOffset? UpdatedAt { get; protected set; }

        protected AuditableEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public void SetUpdated()
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
