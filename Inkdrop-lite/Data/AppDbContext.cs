using InkdropLite.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Notebook> Notebooks => Set<Notebook>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<NoteTag> NoteTags => Set<NoteTag>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notebook>()
                .HasIndex(x => x.Name)
                .IsUnique();

            modelBuilder.Entity<Tag>()
                .HasIndex(x => x.Name)
                .IsUnique();

            modelBuilder.Entity<NoteTag>()
                .HasKey(x => new { x.NoteId, x.TagId });

            modelBuilder.Entity<NoteTag>()
                .HasOne(x => x.Note)
                .WithMany(x => x.NoteTags)
                .HasForeignKey(x => x.NoteId);

            modelBuilder.Entity<NoteTag>()
                .HasOne(x => x.Tag)
                .WithMany(x => x.NoteTags)
                .HasForeignKey(x => x.TagId);
        }
    }
}
