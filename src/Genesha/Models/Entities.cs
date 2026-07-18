namespace Genesha.Models;

public class NoteType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }

    public List<Note> Notes { get; } = new();
}

public class Note
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RelativeFilePath { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? NoteTypeId { get; set; }
    public NoteType? NoteType { get; set; }
}

public class Whiteboard
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RelativeFilePath { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MermaidChart
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RelativeFilePath { get; set; } = "";
    public string? SourceWhiteboardName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
