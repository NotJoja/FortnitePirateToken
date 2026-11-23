using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FortnitePirateToken.Runtime;

/// <summary>Reads files compiled into the plugin DLL (everything under the project's Assets folder).</summary>
internal static class EmbeddedAssets
{
    private static readonly Assembly Assembly = typeof(EmbeddedAssets).Assembly;
    private static readonly string[] Names = Assembly.GetManifestResourceNames();

    public static byte[] ReadBytes(string path)
    {
        using var stream = Open(path);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static string ReadText(string path)
    {
        using var stream = Open(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static Texture2D LoadTexture(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Path.GetFileNameWithoutExtension(path) };
        if (!ImageConversion.LoadImage(tex, ReadBytes(path)))
        {
            throw new InvalidDataException($"Could not decode texture '{path}'.");
        }

        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static Stream Open(string path)
    {
        var wanted = path.Replace('\\', '/');
        var name = Names.FirstOrDefault(n => n.Replace('\\', '/').EndsWith(wanted, StringComparison.OrdinalIgnoreCase));
        if (name == null)
        {
            throw new FileNotFoundException($"Embedded asset '{path}' not found. Available: {string.Join(", ", Names)}");
        }

        return Assembly.GetManifestResourceStream(name)!;
    }
}
