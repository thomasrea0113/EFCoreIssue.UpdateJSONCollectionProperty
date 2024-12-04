
using System.ComponentModel.DataAnnotations;

public class JsonArrayItem
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}

public class Model
{
    public int Id { get; init; }
    public required string Name { get; init; }

    public IReadOnlyList<JsonArrayItem> JsonArray { get; init; } = [];
}