using Inkdrop_lite.Authorization;
using Inkdrop_lite.Domain.Common;
using InkdropLite.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Data
{
    public class AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUser currentUser) : DbContext(options)
    {
        public string? CurrentOwnerId => currentUser.UserId;

        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Notebook> Notebooks => Set<Notebook>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<NoteTag> NoteTags => Set<NoteTag>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notebook>()
                .HasIndex(x => new { x.OwnerId, x.Name })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = 0");

            modelBuilder.Entity<Tag>()
                .HasIndex(x => new { x.OwnerId, x.Name })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = 0");

            modelBuilder.Entity<Note>()
                .HasIndex(x => new { x.OwnerId, x.IsDeleted, x.UpdatedAt });

            modelBuilder.Entity<NoteTag>()
                .HasIndex(x => new { x.OwnerId, x.IsDeleted });

            modelBuilder.Entity<Note>()
                .HasQueryFilter(x => !x.IsDeleted && x.OwnerId == CurrentOwnerId);

            modelBuilder.Entity<Notebook>()
                .HasQueryFilter(x => !x.IsDeleted && x.OwnerId == CurrentOwnerId);

            modelBuilder.Entity<Tag>()
                .HasQueryFilter(x => !x.IsDeleted && x.OwnerId == CurrentOwnerId);

            modelBuilder.Entity<NoteTag>()
                .HasQueryFilter(x => !x.IsDeleted && x.OwnerId == CurrentOwnerId);

            modelBuilder.Entity<Note>().Property(x => x.OwnerId).HasMaxLength(200);
            modelBuilder.Entity<Notebook>().Property(x => x.OwnerId).HasMaxLength(200);
            modelBuilder.Entity<Tag>().Property(x => x.OwnerId).HasMaxLength(200);
            modelBuilder.Entity<NoteTag>().Property(x => x.OwnerId).HasMaxLength(200);

            modelBuilder.Entity<Note>()
                .HasOne(x => x.Notebook)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.NotebookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NoteTag>()
                .HasIndex(x => new { x.OwnerId, x.NoteId, x.TagId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = 0");

            modelBuilder.Entity<NoteTag>()
                .HasOne(x => x.Note)
                .WithMany(x => x.NoteTags)
                .HasForeignKey(x => x.NoteId);

            modelBuilder.Entity<NoteTag>()
                .HasOne(x => x.Tag)
                .WithMany(x => x.NoteTags)
                .HasForeignKey(x => x.TagId);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyOwnershipAndSoftDelete();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            ApplyOwnershipAndSoftDelete();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyOwnershipAndSoftDelete()
        {
            var entries = ChangeTracker.Entries<UserOwnedEntity>()
                .Where(entry => entry.State is
                    EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            if (entries.Count == 0)
            {
                return;
            }

            var ownerId = CurrentOwnerId
                ?? throw new InvalidOperationException(
                    "An authenticated user identifier is required to change owned data.");

            var now = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.OwnerId = ownerId;
                    entry.Entity.CreatedAt = entry.Entity.CreatedAt == default
                        ? now
                        : entry.Entity.CreatedAt;
                    entry.Entity.UpdatedAt = entry.Entity.UpdatedAt == default
                        ? now
                        : entry.Entity.UpdatedAt;
                    entry.Entity.IsDeleted = false;
                    entry.Entity.DeletedAt = null;
                    continue;
                }

                if (!string.Equals(entry.Entity.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Owned data cannot be changed by a different user.");
                }

                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                }

                entry.Property(nameof(UserOwnedEntity.OwnerId)).IsModified = false;
                entry.Property(nameof(UserOwnedEntity.CreatedAt)).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
