namespace InkdropLite.Api.Models;

public class Notebook
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public List<Note> Notes { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
