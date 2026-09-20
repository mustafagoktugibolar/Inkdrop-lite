using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Inkdrop_lite.Features.Notebooks.Contracts;
using Inkdrop_lite.Features.Notes.Contracts;
using Inkdrop_lite.Features.Tags.Contracts;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Tests.Contracts;

public sealed class RequestValidationTests
{
    [Fact]
    public void Note_request_rejects_invalid_title_and_status()
    {
        var request = new CreateNoteRequest(
            new string('x', 201),
            "Content",
            (NoteStatus)999,
            null);

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.Title)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.Status)));
    }

    [Fact]
    public void Note_content_supports_large_markdown_documents()
    {
        var createAttribute = typeof(CreateNoteRequest)
            .GetProperty(nameof(CreateNoteRequest.Content))!
            .GetCustomAttribute<MaxLengthAttribute>();
        var updateAttribute = typeof(UpdateNoteRequest)
            .GetProperty(nameof(UpdateNoteRequest.Content))!
            .GetCustomAttribute<MaxLengthAttribute>();

        Assert.NotNull(createAttribute);
        Assert.NotNull(updateAttribute);
        Assert.Equal(10_000_000, createAttribute.Length);
        Assert.Equal(10_000_000, updateAttribute.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Notebook_name_is_required(string? name)
    {
        var errors = Validate(new CreateNotebookRequest(name!, null));

        Assert.Contains(errors, error => error.MemberNames.Contains("Name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Tag_name_is_required(string? name)
    {
        var errors = Validate(new CreateTagRequest(name!));

        Assert.Contains(errors, error => error.MemberNames.Contains("Name"));
    }

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, true);
        return results;
    }
}
