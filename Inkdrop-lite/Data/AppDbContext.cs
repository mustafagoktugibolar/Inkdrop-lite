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
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();

            // Enums are persisted by name so reordering members never corrupts stored data.
            configurationBuilder.Properties<NoteStatus>().HaveConversion<string>().HaveMaxLength(32);
            configurationBuilder.Properties<TagColor>().HaveConversion<string>().HaveMaxLength(32);
            configurationBuilder.Properties<NotebookIconType>().HaveConversion<string>().HaveMaxLength(32);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notebook>(entity =>
            {
                entity.Property(x => x.Name).HasMaxLength(64);
                entity.Property(x => x.IconSvg).HasMaxLength(262_144);

                entity.HasIndex(x => x.Name);
                entity.HasIndex(x => x.ParentNotebookId);
                entity.HasIndex(x => x.Order);

                entity.HasOne(x => x.ParentNotebook)
                    .WithMany(x => x.ChildNotebooks)
                    .HasForeignKey(x => x.ParentNotebookId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.IconAttachment)
                    .WithMany(x => x.NotebooksUsingAsIcon)
                    .HasForeignKey(x => x.IconAttachmentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Notebooks_NotOwnParent",
                    "\"ParentNotebookId\" IS NULL OR \"ParentNotebookId\" <> \"Id\""));
            });

            modelBuilder.Entity<Note>(entity =>
            {
                entity.Property(x => x.Title).HasMaxLength(256);
                entity.Property(x => x.Content).HasMaxLength(1_048_576);
                entity.Property(x => x.CreatedSource)
                    .HasMaxLength(ChangeSources.MaxLength).HasDefaultValue(ChangeSources.App);
                entity.Property(x => x.UpdatedSource)
                    .HasMaxLength(ChangeSources.MaxLength).HasDefaultValue(ChangeSources.App);

                entity.HasIndex(x => x.NotebookId);
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.UpdatedAt);
                entity.HasIndex(x => x.SourceTemplateId);
                entity.HasIndex(x => new { x.NotebookId, x.UpdatedAt });

                entity.HasOne(x => x.Notebook)
                    .WithMany(x => x.Notes)
                    .HasForeignKey(x => x.NotebookId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.SourceTemplate)
                    .WithMany(x => x.DerivedNotes)
                    .HasForeignKey(x => x.SourceTemplateId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(x => x.Tags)
                    .WithMany(x => x.Notes)
                    .UsingEntity<Dictionary<string, object>>(
                        "NoteTag",
                        right => right.HasOne<Tag>().WithMany()
                            .HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                        left => left.HasOne<Note>().WithMany()
                            .HasForeignKey("NoteId").OnDelete(DeleteBehavior.Cascade),
                        join =>
                        {
                            join.ToTable("NoteTags");
                            join.HasKey("NoteId", "TagId");
                            join.HasIndex("TagId");
                        });

                entity.HasMany(x => x.Attachments)
                    .WithMany(x => x.Notes)
                    .UsingEntity<Dictionary<string, object>>(
                        "NoteAttachment",
                        right => right.HasOne<Attachment>().WithMany()
                            .HasForeignKey("AttachmentId").OnDelete(DeleteBehavior.Cascade),
                        left => left.HasOne<Note>().WithMany()
                            .HasForeignKey("NoteId").OnDelete(DeleteBehavior.Cascade),
                        join =>
                        {
                            join.ToTable("NoteAttachments");
                            join.HasKey("NoteId", "AttachmentId");
                            join.HasIndex("AttachmentId");
                        });

                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Notes_NotOwnTemplate",
                    "\"SourceTemplateId\" IS NULL OR \"SourceTemplateId\" <> \"Id\""));
            });

            modelBuilder.Entity<Tag>(entity =>
            {
                // NOCASE makes the unique index treat RabbitMQ / rabbitmq as the same tag.
                entity.Property(x => x.Name).HasMaxLength(64).UseCollation("NOCASE");

                entity.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique();
                entity.HasIndex(x => x.UpdatedAt);
            });

            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.Property(x => x.Name).HasMaxLength(128);
                entity.Property(x => x.ContentType).HasMaxLength(128);
                entity.Property(x => x.StoragePath).HasMaxLength(512);
                entity.Property(x => x.Hash).HasMaxLength(128);

                entity.HasIndex(x => x.StoragePath).IsUnique();
                entity.HasIndex(x => x.Hash);

                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Attachments_ContentLength",
                    "\"ContentLength\" >= 0"));
            });

            // Tenant isolation: every owned table is scoped to the current user.
            modelBuilder.Entity<Notebook>().HasQueryFilter(x => x.OwnerId == CurrentOwnerId);
            modelBuilder.Entity<Note>().HasQueryFilter(x => x.OwnerId == CurrentOwnerId);
            modelBuilder.Entity<Tag>().HasQueryFilter(x => x.OwnerId == CurrentOwnerId);
            modelBuilder.Entity<Attachment>().HasQueryFilter(x => x.OwnerId == CurrentOwnerId);

            modelBuilder.Entity<Notebook>().Property(x => x.OwnerId).HasMaxLength(200);
            modelBuilder.Entity<Note>().Property(x => x.OwnerId).HasMaxLength(200);
            modelBuilder.Entity<Tag>().Property(x => x.OwnerId).HasMaxLength(200);
            modelBuilder.Entity<Attachment>().Property(x => x.OwnerId).HasMaxLength(200);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyOwnershipAndTimestamps();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            ApplyOwnershipAndTimestamps();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyOwnershipAndTimestamps()
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
            var source = currentUser.ChangeSource is { Length: > 0 } and var label
                ? label[..Math.Min(label.Length, ChangeSources.MaxLength)]
                : ChangeSources.App;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.OwnerId = ownerId;
                    entry.Entity.CreatedAt = entry.Entity.CreatedAt == default
                        ? now
                        : entry.Entity.CreatedAt;

                    if (entry.Entity is UpdatableUserOwnedEntity added)
                    {
                        added.UpdatedAt = added.UpdatedAt == default ? now : added.UpdatedAt;
                    }

                    if (entry.Entity is ISourceTracked createdBy)
                    {
                        createdBy.CreatedSource = source;
                        createdBy.UpdatedSource = source;
                    }

                    continue;
                }

                if (!string.Equals(entry.Entity.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Owned data cannot be changed by a different user.");
                }

                if (entry.State == EntityState.Deleted)
                {
                    continue;
                }

                entry.Property(nameof(UserOwnedEntity.OwnerId)).IsModified = false;
                entry.Property(nameof(UserOwnedEntity.CreatedAt)).IsModified = false;

                if (entry.Entity is UpdatableUserOwnedEntity modified)
                {
                    modified.UpdatedAt = now;
                }

                if (entry.Entity is ISourceTracked updatedBy)
                {
                    updatedBy.UpdatedSource = source;
                    entry.Property(nameof(ISourceTracked.CreatedSource)).IsModified = false;
                }
            }
        }
    }
}
