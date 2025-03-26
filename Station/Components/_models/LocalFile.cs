
using System;

namespace Station.Components._models;

public enum FileType
{
    [FileTypeExtension(".tilt")]
    OpenBrush,
}

[AttributeUsage(AttributeTargets.Field)]
public class FileTypeExtensionAttribute : Attribute
{
    public string? Extension { get; }

    public FileTypeExtensionAttribute(string? extension)
    {
        Extension = extension;
    }
}

public static class FileTypeExtensions
{
    public static string? GetFileExtension(this FileType fileType)
    {
        var type = fileType.GetType();
        var memberInfo = type.GetMember(fileType.ToString());
        var attributes = memberInfo[0].GetCustomAttributes(typeof(FileTypeExtensionAttribute), false);
        return attributes.Length > 0 ? ((FileTypeExtensionAttribute)attributes[0]).Extension : null;
    }
}

public class LocalFile
{
    /// <summary>
    /// The type of file
    /// </summary>
    public readonly FileType fileType;
    
    public readonly string name;
    
    public readonly string path;

    public LocalFile(FileType fileType, string name, string path)
    {
        this.fileType = fileType;
        this.name = name;
        this.path = path;
    }
}
