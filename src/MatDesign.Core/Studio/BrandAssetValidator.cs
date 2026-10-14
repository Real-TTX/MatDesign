using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace MatDesign.Core.Studio;

/// <summary>Which place of the brand a file is for: it decides the types the file may be, its size limit and its proportions.</summary>
public enum AssetSlot
{
    Logo,
    Wordmark,
    Favicon,
    AppIcon,
    Font,
}

/// <summary>What looking at the content of a file found: the type (by content, never by name), its size in px and what is wrong with it. <see cref="Ok"/> is true when nothing is.</summary>
public sealed record AssetInspection(string Ext, int Width, int Height, IReadOnlyList<string> Problems)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// What the files of a brand have to be (IDENT-08), decided before anything reaches the volume and again every time a check runs. The type of a file is decided by its
/// <b>content</b>: the magic bytes of a PNG, an ICO, a WOFF2 or a WOFF, or an SVG that is parsed as XML. The name of the file and the type the browser claimed are never looked at.
/// An SVG is read with an XML reader that forbids a DTD (so no entity can be declared) and every element and attribute must be on a list: no script, no foreignObject, no style
/// element, no image or reference that leaves the document, no handler, nothing that loads another site. A file is refused with the reason, in words a person can act on. Item level: the
/// alt texts, the families and faces of the fonts, the file names, and, with the bytes at hand, that every file is on the volume, still what its name says and still valid.
/// </summary>
public static partial class BrandAssetValidator
{
    /// <summary>The longest SVG, in bytes.</summary>
    public const int MaxSvgBytes = 100 * 1024;

    /// <summary>The longest PNG, in bytes.</summary>
    public const int MaxPngBytes = 500 * 1024;

    /// <summary>The longest ICO, in bytes.</summary>
    public const int MaxIcoBytes = 200 * 1024;

    /// <summary>The longest font file (WOFF2 or WOFF), in bytes.</summary>
    public const int MaxFontBytes = 400 * 1024;

    /// <summary>The most faces a brand may have.</summary>
    public const int MaxFaces = 12;

    /// <summary>The most font families a brand may have.</summary>
    public const int MaxFamilies = 3;

    /// <summary>The widest or tallest PNG, in px.</summary>
    public const int MaxPixels = 2048;

    /// <summary>The widest or tallest an SVG may declare itself (a view box has no unit and is not limited this way), in px.</summary>
    public const int MaxSvgSize = 4096;

    /// <summary>The longest alt text.</summary>
    public const int MaxAltLength = 120;

    /// <summary>The longest family name.</summary>
    public const int MaxFamilyLength = 40;

    /// <summary>The most all the files of a brand may weigh together, counted once each.</summary>
    public const long TotalBudget = 6L * 1024 * 1024;

    /// <summary>The widest a logo or a word mark may be against its height (and the other way round).</summary>
    public const int MaxAspect = 8;

    private const int MaxProblems = 8;
    private const int MaxElements = 4000;
    private const int MaxDepth = 40;
    private const int MaxUses = 40;

    private static readonly string[] GenericFamilies =
    [
        "serif", "sans-serif", "monospace", "cursive", "fantasy", "system-ui", "ui-serif", "ui-sans-serif", "ui-monospace", "ui-rounded", "emoji", "math", "fangsong",
        "inherit", "initial", "unset", "revert", "default", "none", "auto", "normal", "bold", "italic",
    ];

    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
    {
        "svg", "g", "path", "circle", "ellipse", "rect", "line", "polyline", "polygon", "defs", "linearGradient", "radialGradient", "stop", "clipPath", "mask", "title", "desc", "use", "symbol", "text", "tspan", "pattern",
    };

    // elements that are known to run code, load something or change how the page around them looks: named in the refusal, so that the person knows why
    private static readonly Dictionary<string, string> Refused = new(StringComparer.OrdinalIgnoreCase)
    {
        ["script"] = "it runs code",
        ["foreignObject"] = "it embeds HTML",
        ["style"] = "it carries CSS (put the colours into fill and stroke attributes)",
        ["image"] = "it loads a picture from somewhere else",
        ["a"] = "it links away",
        ["animate"] = "it animates attributes and can set a link",
        ["animateTransform"] = "it animates",
        ["animateMotion"] = "it animates",
        ["set"] = "it can change an attribute to a link",
        ["filter"] = "a filter can load a picture",
        ["feImage"] = "it loads a picture",
        ["iframe"] = "it embeds a page",
        ["embed"] = "it embeds a plug-in",
        ["object"] = "it embeds a file",
        ["audio"] = "it plays sound",
        ["video"] = "it plays video",
        ["link"] = "it loads a resource",
        ["meta"] = "it is page markup",
        ["handler"] = "it runs code",
        ["listener"] = "it runs code",
    };

    private static readonly HashSet<string> Attributes = new(StringComparer.Ordinal)
    {
        "id", "class", "viewBox", "width", "height", "x", "y", "cx", "cy", "r", "rx", "ry", "fx", "fy", "x1", "y1", "x2", "y2", "d", "points", "transform",
        "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "stroke-opacity",
        "opacity", "stop-color", "stop-opacity", "offset", "gradientUnits", "gradientTransform", "spreadMethod", "clip-path", "clip-rule", "clipPathUnits", "mask", "maskUnits", "maskContentUnits",
        "patternUnits", "patternTransform", "patternContentUnits", "preserveAspectRatio", "version", "role", "aria-label", "aria-hidden", "aria-labelledby", "focusable", "style",
        "font-family", "font-size", "font-weight", "font-style", "text-anchor", "dominant-baseline", "letter-spacing", "dx", "dy", "visibility", "display", "shape-rendering", "href",
    };

    private static readonly HashSet<string> StyleProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "stroke-opacity",
        "opacity", "stop-color", "stop-opacity", "clip-rule", "display", "visibility", "font-family", "font-size", "font-weight", "font-style", "text-anchor", "letter-spacing", "shape-rendering",
    };

    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private const string XlinkNamespace = "http://www.w3.org/1999/xlink";
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    [GeneratedRegex(@"^[0-9a-f]{64}\.(?:svg|png|ico|woff2|woff)$")]
    private static partial Regex StoredName();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9 -]*$")]
    private static partial Regex FamilyPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:javascript|vbscript|data)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptScheme();

    [GeneratedRegex(@"url\s*\(\s*(?<q>['""]?)\s*(?<target>[^)'""]*)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlFunction();

    [GeneratedRegex(@"^#[A-Za-z_][A-Za-z0-9_.:-]*$")]
    private static partial Regex Fragment();

    [GeneratedRegex(@"^\s*([0-9]*\.?[0-9]+)\s*(px|pt|em|mm|cm|in|%)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Length();

    [GeneratedRegex(@"^[A-Za-z0-9#%.,\s()_+-]*$")]
    private static partial Regex StyleValue();

    // ------------------------------------------------------------------------------------------------ names

    /// <summary>The Slot a name of the forms and files stands for: <c>logo</c> and <c>logo-dark</c> are logos, and so on.</summary>
    public static AssetSlot? SlotOf(string? slot) => slot switch
    {
        "logo" or "logo-dark" => AssetSlot.Logo,
        "wordmark" or "wordmark-dark" => AssetSlot.Wordmark,
        "favicon" => AssetSlot.Favicon,
        "app-icon" => AssetSlot.AppIcon,
        "font" => AssetSlot.Font,
        _ => null,
    };

    /// <summary>The types (as the extension) a slot may hold.</summary>
    public static IReadOnlyList<string> AllowedTypes(AssetSlot slot) => slot switch
    {
        AssetSlot.Logo or AssetSlot.Wordmark => [".svg", ".png"],
        AssetSlot.Favicon => [".svg", ".png", ".ico"],
        AssetSlot.AppIcon => [".png"],
        _ => [".woff2", ".woff"],
    };

    /// <summary>The longest file of a type, in bytes.</summary>
    public static int MaxBytes(string ext) => ext switch
    {
        ".svg" => MaxSvgBytes,
        ".png" => MaxPngBytes,
        ".ico" => MaxIcoBytes,
        _ => MaxFontBytes,
    };

    /// <summary>The SHA-256 of a content in lower-case hex.</summary>
    public static string Sha256(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    /// <summary>The name a content is stored under: its hash and the extension of its type.</summary>
    public static string StoredFileName(ReadOnlySpan<byte> content, string ext) => Sha256(content) + ext;

    /// <summary>True when a name is the name of a stored file: 64 hex digits and one of the five extensions, never a path.</summary>
    public static bool IsStoredName(string? name) => name is not null && StoredName().IsMatch(name);

    /// <summary>The name of a family in a file name: lower case, letters and digits, words joined by a dash.</summary>
    public static string Slug(string family) => string.Join('-', family.Trim().ToLowerInvariant().Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The family names in a font stack (<c>"Segoe UI", Roboto, sans-serif</c>), without quotes: what the toolkit already uses by name.</summary>
    public static IEnumerable<string> FamiliesIn(string stack)
        => stack.Split(',').Select(p => p.Trim().Trim('"', '\'')).Where(p => p.Length > 0);

    // ------------------------------------------------------------------------------------------------ a file

    /// <summary>
    /// Looks at the bytes and says what they are and what is wrong with them for a slot. The type comes from the content; a file that is not a PNG, an ICO, a WOFF2, a WOFF or an SVG is refused
    /// whatever its name or its claimed type was.
    /// </summary>
    public static AssetInspection Inspect(byte[] content, AssetSlot slot)
    {
        var problems = new List<string>();
        if (content is null || content.Length == 0)
        {
            return new AssetInspection(string.Empty, 0, 0, ["The file is empty."]);
        }

        var ext = Detect(content);
        if (ext is null)
        {
            return new AssetInspection(string.Empty, 0, 0,
                ["This is not a file a brand may use. It must be a PNG, an ICO, a WOFF2, a WOFF or an SVG, judged by what is inside it: the name of the file and the type your browser sent do not count."]);
        }

        if (!AllowedTypes(slot).Contains(ext))
        {
            problems.Add($"This file is {Describe(ext)}, but {SlotName(slot)} must be {AllowedList(slot)}.");
            return new AssetInspection(ext, 0, 0, problems);
        }

        if (content.Length > MaxBytes(ext))
        {
            problems.Add($"The file is {Kb(content.Length)}; {Describe(ext)} may be {Kb(MaxBytes(ext))} at most.");
            return new AssetInspection(ext, 0, 0, problems);
        }

        int width = 0, height = 0;
        switch (ext)
        {
            case ".png":
                PngProblems(content, problems, out width, out height);
                break;
            case ".ico":
                IcoProblems(content, problems, out width, out height);
                break;
            case ".svg":
                SvgProblems(content, problems, out width, out height);
                break;
            default:
                FontProblems(content, ext, problems);
                break;
        }

        if (problems.Count == 0 && ext is ".png" or ".svg")
        {
            ShapeProblems(slot, ext, width, height, problems);
        }

        return new AssetInspection(ext, width, height, problems);
    }

    private static string Describe(string ext) => ext switch
    {
        ".svg" => "an SVG",
        ".png" => "a PNG",
        ".ico" => "an ICO",
        ".woff2" => "a WOFF2 font",
        _ => "a WOFF font",
    };

    private static string SlotName(AssetSlot slot) => slot switch
    {
        AssetSlot.Logo => "a logo",
        AssetSlot.Wordmark => "a word mark",
        AssetSlot.Favicon => "a favicon",
        AssetSlot.AppIcon => "an app icon",
        _ => "a font",
    };

    private static string AllowedList(AssetSlot slot) => string.Join(" or ", AllowedTypes(slot).Select(t => Describe(t).Replace(" font", string.Empty, StringComparison.Ordinal)));

    private static string Kb(long bytes) => bytes < 1024 ? $"{bytes} bytes" : string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB");

    private static string? Detect(byte[] c)
    {
        if (c.Length >= 8 && c.AsSpan(0, 8).SequenceEqual(PngSignature))
        {
            return ".png";
        }

        if (c.Length >= 4 && c[0] == 0 && c[1] == 0 && c[2] == 1 && c[3] == 0)
        {
            return ".ico";
        }

        if (c.Length >= 4 && c[0] == 'w' && c[1] == 'O' && c[2] == 'F' && c[3] == '2')
        {
            return ".woff2";
        }

        if (c.Length >= 4 && c[0] == 'w' && c[1] == 'O' && c[2] == 'F' && c[3] == 'F')
        {
            return ".woff";
        }

        // text: a byte order mark and white space, then the opening angle bracket of the XML declaration or of the root element; whether it is an SVG is for the parser to say
        var i = 0;
        if (c.Length >= 3 && c[0] == 0xEF && c[1] == 0xBB && c[2] == 0xBF)
        {
            i = 3;
        }

        while (i < c.Length && c[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            i++;
        }

        return i < c.Length && c[i] == '<' ? ".svg" : null;
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The proportions a slot allows. A logo may be wide (up to 8 to 1), a favicon and an app icon are square.</summary>
    private static void ShapeProblems(AssetSlot slot, string ext, int width, int height, List<string> problems)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        switch (slot)
        {
            case AssetSlot.Logo or AssetSlot.Wordmark:
                if (width > height * MaxAspect || height > width * MaxAspect)
                {
                    problems.Add($"The proportions are {width} x {height}: a logo may be at most {MaxAspect} times wider than tall (or the other way round).");
                }

                if (ext == ".png" && (width < 16 || height < 16))
                {
                    problems.Add($"The picture is {width} x {height} px; at least 16 px each way.");
                }

                break;
            case AssetSlot.Favicon when ext == ".png":
                if (width != height || width is < 16 or > 512)
                {
                    problems.Add($"A PNG favicon is square and 16 to 512 px; this one is {width} x {height}.");
                }

                break;
            case AssetSlot.Favicon:
                if (ext == ".svg" && (width > height * 2 || height > width * 2))
                {
                    problems.Add($"A favicon is square: this SVG is {width} x {height}.");
                }

                break;
            case AssetSlot.AppIcon:
                if (width != 512 || height != 512)
                {
                    problems.Add($"An app icon is a PNG of exactly 512 x 512 px; this one is {width} x {height}.");
                }

                break;
        }
    }

    // ------------------------------------------------------------------------------------------------ PNG

    private static void PngProblems(byte[] c, List<string> problems, out int width, out int height)
    {
        width = height = 0;
        var pos = 8;
        var first = true;
        var ended = false;
        var sawData = false;
        while (pos < c.Length && !ended)
        {
            if (pos + 12 > c.Length)
            {
                problems.Add("The PNG is cut off: a chunk is incomplete.");
                return;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(pos, 4));
            if (length > c.Length - pos - 12)
            {
                problems.Add("The PNG is damaged: a chunk is longer than the file.");
                return;
            }

            var type = Encoding.ASCII.GetString(c, pos + 4, 4);
            if (type.Any(ch => !char.IsAsciiLetter(ch)))
            {
                problems.Add("The PNG is damaged: a chunk has no valid name.");
                return;
            }

            var crc = BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(pos + 8 + (int)length, 4));
            if (Crc32(c.AsSpan(pos + 4, 4 + (int)length)) != crc)
            {
                problems.Add($"The PNG is damaged: the checksum of the chunk {type} is wrong.");
                return;
            }

            if (first)
            {
                if (type != "IHDR" || length != 13)
                {
                    problems.Add("The PNG is damaged: it does not start with its header.");
                    return;
                }

                width = (int)BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(pos + 8, 4));
                height = (int)BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(pos + 12, 4));
                first = false;
            }

            sawData |= type == "IDAT";
            ended = type == "IEND";
            pos += 12 + (int)length;
        }

        if (!ended)
        {
            problems.Add("The PNG is cut off: it has no end.");
        }
        else if (pos != c.Length)
        {
            problems.Add("There is data after the end of the PNG. A picture is only a picture.");
        }

        if (!sawData && problems.Count == 0)
        {
            problems.Add("The PNG has no picture data.");
        }

        if (problems.Count == 0 && (width is <= 0 or > MaxPixels || height is <= 0 or > MaxPixels))
        {
            problems.Add($"The picture is {width} x {height} px; {MaxPixels} px each way at most.");
        }
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }

        return c;
    }).ToArray();

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    // ------------------------------------------------------------------------------------------------ ICO

    private static void IcoProblems(byte[] c, List<string> problems, out int width, out int height)
    {
        width = height = 0;
        if (c.Length < 22)
        {
            problems.Add("The ICO is cut off: it is too short to hold an icon.");
            return;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(c.AsSpan(4, 2));
        if (count is < 1 or > 32 || 6 + (count * 16) > c.Length)
        {
            problems.Add("The ICO is damaged: it has no usable list of images.");
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var entry = 6 + (i * 16);
            var w = c[entry] == 0 ? 256 : c[entry];
            var h = c[entry + 1] == 0 ? 256 : c[entry + 1];
            var size = BinaryPrimitives.ReadUInt32LittleEndian(c.AsSpan(entry + 8, 4));
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(c.AsSpan(entry + 12, 4));
            if (size == 0 || offset < 6 + (count * 16) || (long)offset + size > c.Length)
            {
                problems.Add("The ICO is damaged: an image lies outside the file.");
                return;
            }

            var isPng = size >= 8 && c.AsSpan((int)offset, 8).SequenceEqual(PngSignature);
            var isBitmap = size >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(c.AsSpan((int)offset, 4)) == 40;
            if (!isPng && !isBitmap)
            {
                problems.Add("The ICO is damaged: an image in it is neither a PNG nor a bitmap.");
                return;
            }

            width = Math.Max(width, w);
            height = Math.Max(height, h);
        }
    }

    // ------------------------------------------------------------------------------------------------ fonts

    private static void FontProblems(byte[] c, string ext, List<string> problems)
    {
        var woff2 = ext == ".woff2";
        var header = woff2 ? 48 : 44;
        if (c.Length < header)
        {
            problems.Add($"The {(woff2 ? "WOFF2" : "WOFF")} file is cut off: it is shorter than its header.");
            return;
        }

        var flavor = BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(4, 4));
        var declared = BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(8, 4));
        var tables = BinaryPrimitives.ReadUInt16BigEndian(c.AsSpan(12, 2));
        uint[] flavors = woff2 ? [0x00010000, 0x4F54544F, 0x74746366] : [0x00010000, 0x4F54544F, 0x74727565];
        if (!flavors.Contains(flavor))
        {
            problems.Add("The font is not a TrueType or OpenType font inside.");
        }

        if (declared != c.Length)
        {
            problems.Add("The font is damaged or cut off: the length in its header is not the length of the file.");
        }

        if (tables is 0 or > 128)
        {
            problems.Add("The font is damaged: it has no usable list of tables.");
        }

        if (!woff2 && problems.Count == 0)
        {
            for (var i = 0; i < tables; i++)
            {
                var entry = 44 + (i * 20);
                if (entry + 20 > c.Length || (long)BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(entry + 4, 4)) + BinaryPrimitives.ReadUInt32BigEndian(c.AsSpan(entry + 8, 4)) > c.Length)
                {
                    problems.Add("The font is damaged: a table lies outside the file.");
                    return;
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ SVG

    private static void SvgProblems(byte[] c, List<string> problems, out int width, out int height)
    {
        width = height = 0;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 400_000,
            MaxCharactersFromEntities = 0,
            IgnoreWhitespace = false,
            CloseInput = true,
        };

        var elements = 0;
        var uses = 0;
        var depth = 0;
        var seenRoot = false;
        string? declaredWidth = null, declaredHeight = null, viewBox = null;
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(c), settings);
            while (reader.Read())
            {
                if (problems.Count >= MaxProblems)
                {
                    break;
                }

                switch (reader.NodeType)
                {
                    case XmlNodeType.ProcessingInstruction:
                        problems.Add($"The SVG has a processing instruction (<?{reader.Name} ...?>), for example a style sheet. That is not allowed.");
                        break;
                    case XmlNodeType.Element:
                        elements++;
                        depth = reader.Depth + 1;
                        if (elements > MaxElements || depth > MaxDepth)
                        {
                            problems.Add($"The SVG is too complex ({MaxElements} elements and {MaxDepth} levels at most).");
                            return;
                        }

                        var root = !seenRoot;
                        seenRoot = true;
                        CheckElement(reader, root, problems, ref uses, out var w, out var h, out var vb);
                        if (root)
                        {
                            declaredWidth = w;
                            declaredHeight = h;
                            viewBox = vb;
                        }

                        break;
                    case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Comment or XmlNodeType.SignificantWhitespace:
                        if (ScriptScheme().IsMatch(StripSpaces(reader.Value)))
                        {
                            problems.Add("The SVG has text that looks like a script or data address (javascript: or data:). That is not allowed anywhere in it.");
                        }

                        break;
                }
            }
        }
        catch (XmlException ex)
        {
            // the reader refuses a DOCTYPE by itself; the bytes are looked at too, so that the reason does not depend on the wording of the exception
            var declaresDtd = ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase) || Encoding.UTF8.GetString(c).Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || Encoding.UTF8.GetString(c).Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase);
            problems.Add(declaresDtd
                ? "The SVG declares a DTD or an entity (<!DOCTYPE ...>). That is not allowed: it is how a file is made to load another file or to blow up in memory."
                : "This is not a well-formed SVG file (" + FirstLine(ex.Message) + ").");
            return;
        }

        if (!seenRoot)
        {
            problems.Add("The file has no SVG in it.");
        }

        if (problems.Count > 0)
        {
            return;
        }

        SizeOf(declaredWidth, declaredHeight, viewBox, problems, out width, out height);
    }

    private static string FirstLine(string text) => text.Split('\n')[0].Trim();

    private static void CheckElement(XmlReader reader, bool root, List<string> problems, ref int uses, out string? width, out string? height, out string? viewBox)
    {
        width = height = viewBox = null;
        var name = reader.LocalName;
        if (root && (name != "svg" || reader.NamespaceURI != SvgNamespace))
        {
            problems.Add("The root element is not an SVG (<svg xmlns=\"http://www.w3.org/2000/svg\" ...>).");
            return;
        }

        if (reader.NamespaceURI != SvgNamespace)
        {
            problems.Add($"The element <{reader.Name}> is not an SVG element. Elements and attributes of other namespaces (inkscape:, sodipodi:, rdf: and the like) are not allowed: save as plain SVG.");
        }
        else if (Refused.TryGetValue(name, out var why) && !Elements.Contains(name))
        {
            problems.Add($"The SVG uses <{name}>, which is not allowed: {why}.");
        }
        else if (!Elements.Contains(name))
        {
            problems.Add($"The SVG uses <{name}>, which is not on the list of elements a brand may use ({string.Join(", ", Elements.Order(StringComparer.Ordinal))}).");
        }
        else if (name == "use" && ++uses > MaxUses)
        {
            problems.Add($"The SVG has more than {MaxUses} <use> elements.");
        }

        while (reader.MoveToNextAttribute())
        {
            var attribute = reader.Name;
            var value = reader.Value;
            if (value.Length > 60_000)
            {
                problems.Add($"The attribute {attribute} is too long.");
                continue;
            }

            if (attribute.StartsWith("on", StringComparison.OrdinalIgnoreCase) || reader.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"The attribute {attribute} is an event handler (it runs code). That is not allowed.");
                continue;
            }

            if (reader.NamespaceURI == XmlnsNamespace || attribute == "xmlns")
            {
                if (value != SvgNamespace && value != XlinkNamespace)
                {
                    problems.Add($"The namespace declaration {attribute}=\"{Clip(value)}\" is not allowed: only the SVG and xlink namespaces are.");
                }

                continue;
            }

            if (reader.NamespaceURI == XmlNamespace && reader.LocalName == "space")
            {
                continue;
            }

            var isHref = reader.LocalName == "href" && (reader.NamespaceURI is "" or XlinkNamespace);
            if (reader.NamespaceURI is not "" && !isHref)
            {
                problems.Add($"The attribute {attribute} is of another namespace. Only plain SVG attributes are allowed: save as plain SVG.");
                continue;
            }

            if (!isHref && !Attributes.Contains(attribute))
            {
                problems.Add($"The attribute {attribute} is not on the list of attributes a brand may use.");
                continue;
            }

            if (isHref)
            {
                if (!Fragment().IsMatch(value))
                {
                    problems.Add($"The reference {attribute}=\"{Clip(value)}\" leads out of the document. Only references to a part of the same file (#id) are allowed.");
                }

                continue;
            }

            if (ValueProblem(attribute, value) is { } problem)
            {
                problems.Add(problem);
                continue;
            }

            if (attribute == "style" && StyleProblem(value) is { } style)
            {
                problems.Add(style);
            }

            if (root)
            {
                switch (attribute)
                {
                    case "width":
                        width = value;
                        break;
                    case "height":
                        height = value;
                        break;
                    case "viewBox":
                        viewBox = value;
                        break;
                }
            }
        }

        reader.MoveToElement();
    }

    private static string Clip(string value) => value.Length <= 40 ? value : value[..40] + "...";

    private static string StripSpaces(string value) => new(value.Where(ch => !char.IsWhiteSpace(ch) && !char.IsControl(ch)).ToArray());

    /// <summary>What is wrong with the value of an attribute that nothing may hold: a script or data address, or a <c>url(</c> that does not point into the file.</summary>
    private static string? ValueProblem(string attribute, string value)
    {
        if (ScriptScheme().IsMatch(StripSpaces(value)))
        {
            return $"The attribute {attribute} holds a script or data address (javascript: or data:). That is not allowed anywhere in an SVG.";
        }

        foreach (Match url in UrlFunction().Matches(value))
        {
            if (!Fragment().IsMatch(url.Groups["target"].Value.Trim()))
            {
                return $"The attribute {attribute} has a url( that does not point into the file (url(#id) is the only kind allowed).";
            }
        }

        return null;
    }

    /// <summary>The style attribute is read declaration by declaration: only presentation properties, with plain values.</summary>
    private static string? StyleProblem(string style)
    {
        if (style.Contains('\\', StringComparison.Ordinal) || style.Contains("/*", StringComparison.Ordinal) || style.Contains("@", StringComparison.Ordinal) || style.Contains("expression", StringComparison.OrdinalIgnoreCase))
        {
            return "The style attribute has an escape, a comment, an @-rule or an expression. Use plain declarations (fill:#fff;stroke:none) or attributes.";
        }

        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
            {
                return $"The style declaration '{Clip(declaration)}' is not a property and a value.";
            }

            var property = declaration[..colon].Trim();
            var value = declaration[(colon + 1)..].Trim();
            if (!StyleProperties.Contains(property))
            {
                return $"The style property {property} is not allowed (only fill, stroke, opacity, text and similar presentation properties).";
            }

            if (!StyleValue().IsMatch(value))
            {
                return $"The value of the style property {property} has characters that are not allowed.";
            }
        }

        return null;
    }

    private static void SizeOf(string? declaredWidth, string? declaredHeight, string? viewBox, List<string> problems, out int width, out int height)
    {
        width = height = 0;
        double? w = PixelsOf(declaredWidth, problems), h = PixelsOf(declaredHeight, problems);
        if (problems.Count > 0)
        {
            return;
        }

        double? vw = null, vh = null;
        if (viewBox is not null)
        {
            var parts = viewBox.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || parts.Any(p => !double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || Math.Abs(n) > 1_000_000))
            {
                problems.Add("The viewBox is not four numbers.");
                return;
            }

            vw = double.Parse(parts[2], CultureInfo.InvariantCulture);
            vh = double.Parse(parts[3], CultureInfo.InvariantCulture);
            if (vw <= 0 || vh <= 0)
            {
                problems.Add("The viewBox has no width or height.");
                return;
            }
        }

        if (vw is null && (w is null || h is null))
        {
            problems.Add("The SVG has no viewBox and no width and height, so it has no size and cannot be scaled.");
            return;
        }

        width = (int)Math.Round(w ?? vw!.Value);
        height = (int)Math.Round(h ?? vh!.Value);
        if (w is not null && h is null && vw is not null)
        {
            height = (int)Math.Round(w.Value * vh!.Value / vw.Value);
        }
        else if (h is not null && w is null && vw is not null)
        {
            width = (int)Math.Round(h.Value * vw.Value / vh!.Value);
        }

        if (width <= 0 || height <= 0)
        {
            problems.Add("The SVG has no size.");
        }
    }

    private static double? PixelsOf(string? length, List<string> problems)
    {
        if (length is null)
        {
            return null;
        }

        var m = Length().Match(length);
        if (!m.Success)
        {
            problems.Add($"The size '{Clip(length)}' is not a length.");
            return null;
        }

        if (m.Groups[2].Value == "%")
        {
            return null;
        }

        var number = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var px = m.Groups[2].Value.ToLowerInvariant() switch
        {
            "pt" => number * 4 / 3,
            "em" => number * 16,
            "mm" => number * 96 / 25.4,
            "cm" => number * 96 / 2.54,
            "in" => number * 96,
            _ => number,
        };
        if (px > MaxSvgSize)
        {
            problems.Add($"The SVG declares a size of {Math.Round(px)} px; {MaxSvgSize} px each way at most (give it a viewBox and let it scale).");
            return null;
        }

        return px;
    }

    // ------------------------------------------------------------------------------------------------ the item

    /// <summary>Why a family name is not allowed; null when it is. Letters, digits, spaces and dashes only; not a generic family; not one the toolkit's font stacks already name.</summary>
    public static string? CheckFamily(string? family, IEnumerable<string> reserved)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return "Name the family.";
        }

        var name = family.Trim();
        if (name.Length > MaxFamilyLength || !FamilyPattern().IsMatch(name) || name.Contains("  ", StringComparison.Ordinal) || name.EndsWith(' ') || name.EndsWith('-'))
        {
            return $"A family name starts with a letter and has letters, digits, spaces and dashes only ({MaxFamilyLength} characters at most).";
        }

        if (GenericFamilies.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return $"'{name}' is a generic family of CSS: choose a name of your own.";
        }

        if (reserved.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return $"'{name}' is a family the toolkit's own font stacks already name: a brand's font must not take the place of one.";
        }

        return null;
    }

    /// <summary>The alt text a logo or a word mark needs: said in words, 1 to 120 characters, no control characters.</summary>
    public static string? CheckAlt(string? alt)
    {
        if (string.IsNullOrWhiteSpace(alt))
        {
            return "Write the alt text: what a person who cannot see the image should hear (the name of the company).";
        }

        if (alt.Trim().Length > MaxAltLength || alt.Any(char.IsControl))
        {
            return $"The alt text is one line of {MaxAltLength} characters at most.";
        }

        return null;
    }

    /// <summary>The assets as the validator wants to see them: nothing null, text trimmed, an alt text only with its image, the style of a face in lower case.</summary>
    public static BrandAssets Normalize(BrandAssets assets)
    {
        string Alt(string? alt, AssetFile? file) => file is null ? string.Empty : Regex.Replace((alt ?? string.Empty).Trim(), @"\s+", " ");
        var fonts = (assets.Fonts ?? []).Where(f => f is not null).Select(f => f with
        {
            Family = Regex.Replace((f.Family ?? string.Empty).Trim(), @" {2,}", " "),
            Style = (f.Style ?? string.Empty).Trim().ToLowerInvariant(),
            File = (f.File ?? string.Empty).Trim().ToLowerInvariant(),
        }).ToList();
        return assets with
        {
            LogoDark = assets.Logo is null ? null : assets.LogoDark,
            WordmarkDark = assets.Wordmark is null ? null : assets.WordmarkDark,
            LogoAlt = Alt(assets.LogoAlt, assets.Logo),
            WordmarkAlt = Alt(assets.WordmarkAlt, assets.Wordmark),
            Fonts = fonts,
        };
    }

    /// <summary>
    /// What is wrong with the assets as an item, without looking at a file: the alt texts, the file names and their types per slot, the dark variants (which need their light ones), the faces
    /// (weight, style, family, no face twice, at most 12 faces in at most 3 families). <paramref name="reservedFamilies"/> are the families the toolkit's stacks name.
    /// </summary>
    public static IReadOnlyList<Problem> Validate(BrandAssets assets, IEnumerable<string> reservedFamilies)
    {
        var problems = new List<Problem>();
        var reserved = reservedFamilies.ToList();
        foreach (var (slot, field) in new[] { ("logo", "Logo"), ("logo-dark", "LogoDark"), ("wordmark", "Wordmark"), ("wordmark-dark", "WordmarkDark"), ("favicon", "Favicon"), ("app-icon", "AppIcon") })
        {
            if (assets.In(slot) is not { } file)
            {
                continue;
            }

            if (NameProblem(file.File, SlotOf(slot)!.Value) is { } why)
            {
                problems.Add(new Problem(field, why));
            }
        }

        if (assets.LogoDark is not null && assets.Logo is null)
        {
            problems.Add(new Problem("LogoDark", "A logo for dark surfaces needs the logo it belongs to."));
        }

        if (assets.WordmarkDark is not null && assets.Wordmark is null)
        {
            problems.Add(new Problem("WordmarkDark", "A word mark for dark surfaces needs the word mark it belongs to."));
        }

        if (assets.Logo is not null && CheckAlt(assets.LogoAlt) is { } logoAlt)
        {
            problems.Add(new Problem("LogoAlt", logoAlt));
        }

        if (assets.Wordmark is not null && CheckAlt(assets.WordmarkAlt) is { } wordAlt)
        {
            problems.Add(new Problem("WordmarkAlt", wordAlt));
        }

        var faces = assets.Faces;
        if (faces.Count > MaxFaces)
        {
            problems.Add(new Problem("Fonts", $"A brand has {MaxFaces} font faces at most; there are {faces.Count}."));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in faces)
        {
            var label = $"{face.Family} {face.Weight} {face.Style}";
            if (CheckFamily(face.Family, reserved) is { } familyProblem)
            {
                problems.Add(new Problem("Fonts", $"{label}: {familyProblem}"));
            }

            if (face.Weight is < 100 or > 900 || face.Weight % 100 != 0)
            {
                problems.Add(new Problem("Fonts", $"{label}: the weight is 100, 200 ... 900."));
            }

            if (face.Style is not ("normal" or "italic"))
            {
                problems.Add(new Problem("Fonts", $"{label}: the style is normal or italic."));
            }

            if (NameProblem(face.File, AssetSlot.Font) is { } fileProblem)
            {
                problems.Add(new Problem("Fonts", $"{label}: {fileProblem}"));
            }

            if (!seen.Add($"{(face.Family ?? string.Empty).Trim()}|{face.Weight}|{face.Style}"))
            {
                problems.Add(new Problem("Fonts", $"{label} is there twice: a family has one file for each weight and style."));
            }
        }

        var families = faces.Select(f => (f.Family ?? string.Empty).Trim().ToLowerInvariant()).Distinct().Count();
        if (families > MaxFamilies)
        {
            problems.Add(new Problem("Fonts", $"A brand has {MaxFamilies} font families at most; there are {families}."));
        }

        return problems;
    }

    private static string? NameProblem(string? file, AssetSlot slot)
    {
        if (!IsStoredName(file))
        {
            return "The file is not a stored file of the brand (the name of a file is its SHA-256 and its extension).";
        }

        if (AllowedTypes(slot).Contains(Extension(file!)))
        {
            return null;
        }

        var what = SlotName(slot);
        return $"{char.ToUpperInvariant(what[0])}{what[1..]} is {AllowedList(slot)}, not {Describe(Extension(file!))}.";
    }

    private static string Extension(string file) => BrandAssets.Extension(file);

    /// <summary>
    /// <see cref="Validate"/> and, for every file the item refers to, what the bytes say: it is on the volume, its content still has the hash in its name, it is still valid for its slot
    /// and the files together are within the budget. <paramref name="read"/> gives the bytes of a stored file, or null when it is not there.
    /// </summary>
    public static IReadOnlyList<Problem> ValidateFiles(BrandAssets assets, Func<string, byte[]?> read, IEnumerable<string> reservedFamilies)
    {
        var problems = Validate(assets, reservedFamilies).ToList();
        long total = 0;
        var counted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in assets.References())
        {
            var field = reference.Slot switch
            {
                "logo" => "Logo",
                "logo-dark" => "LogoDark",
                "wordmark" => "Wordmark",
                "wordmark-dark" => "WordmarkDark",
                "favicon" => "Favicon",
                "app-icon" => "AppIcon",
                _ => "Fonts",
            };
            if (!IsStoredName(reference.File))
            {
                continue;
            }

            var bytes = read(reference.File);
            if (bytes is null)
            {
                problems.Add(new Problem(field, $"{reference.Name}: the file {BrandAssets.ShortHash(reference.File)}... is missing from the volume."));
                continue;
            }

            if (counted.Add(reference.File))
            {
                total += bytes.Length;
            }

            if (!Sha256(bytes).Equals(reference.File[..64], StringComparison.Ordinal))
            {
                problems.Add(new Problem(field, $"{reference.Name}: the content does not have the hash in the name of the file any more. The file was changed on the volume."));
                continue;
            }

            var inspection = Inspect(bytes, SlotOf(reference.Slot)!.Value);
            problems.AddRange(inspection.Problems.Select(p => new Problem(field, $"{reference.Name}: {p}")));
            if (inspection.Ok && inspection.Ext != Extension(reference.File))
            {
                problems.Add(new Problem(field, $"{reference.Name}: the content is {Describe(inspection.Ext)}, not what its extension says."));
            }
        }

        if (total > TotalBudget)
        {
            problems.Add(new Problem("Budget", $"The files weigh {Kb(total)} together; a brand may have {Kb(TotalBudget)}."));
        }

        return problems;
    }

    /// <summary>True when the assets can be written into a stylesheet and a menu without harm (names, families, weights and styles are of the shape they must be). Runs again for an item read from the volume.</summary>
    public static bool IsSafe(BrandAssets? assets)
    {
        if (assets is null)
        {
            return false;
        }

        foreach (var slot in BrandAssets.Slots)
        {
            if (assets.In(slot) is { } file && NameProblem(file.File, SlotOf(slot)!.Value) is not null)
            {
                return false;
            }
        }

        if (assets.Faces.Count > MaxFaces || (assets.LogoAlt?.Length ?? 0) > MaxAltLength * 2 || (assets.WordmarkAlt?.Length ?? 0) > MaxAltLength * 2)
        {
            return false;
        }

        return assets.Faces.All(f => f is not null && f.Family is not null && f.Family.Length <= MaxFamilyLength && FamilyPattern().IsMatch(f.Family) && f.Weight is >= 100 and <= 900
            && f.Style is "normal" or "italic" && NameProblem(f.File, AssetSlot.Font) is null);
    }
}
