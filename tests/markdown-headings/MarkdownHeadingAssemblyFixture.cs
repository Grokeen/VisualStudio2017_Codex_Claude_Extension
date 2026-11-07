using System;
using System.IO;
using System.Reflection;

internal static class MarkdownHeadingAssemblyFixture
{
    private static int passed;
    private static MethodInfo apply;
    private static MethodInfo isSetext;

    private static void Equal(string name, string expected, string actual)
    {
        if (!String.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException(name + " failed. Expected <" + Escape(expected) + ">, got <" + Escape(actual) + ">.");
        passed++;
    }

    private static string Escape(string value)
    {
        return (value ?? "<null>").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name + " failed.");
        passed++;
    }

    private static string Apply(string text, int level, bool addMarkerToSingleBlankLine)
    {
        try { return (string)apply.Invoke(null, new object[] { text, level, addMarkerToSingleBlankLine }); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static bool IsSetext(string text)
    {
        try { return (bool)isSetext.Invoke(null, new object[] { text }); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0])) throw new ArgumentException("Pass one product assembly path.");
        string assemblyPath = Path.GetFullPath(args[0]);
        var assembly = Assembly.LoadFrom(assemblyPath);
        var type = assembly.GetType("VS2017.AIAssistant.Views.MarkdownHeadingEditing", true, false);
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        apply = type.GetMethod("Apply", flags, null, new Type[] { typeof(string), typeof(int), typeof(bool) }, null);
        isSetext = type.GetMethod("IsSetextUnderline", flags, null, new Type[] { typeof(string) }, null);
        if (apply == null || isSetext == null) throw new MissingMethodException(type.FullName, "Apply/IsSetextUnderline");

        Equal("plain line h1", "# Summary", Apply("Summary", 1, false));
        Equal("plain line h2", "## Summary", Apply("Summary", 2, false));
        Equal("ATX level replacement", "### Summary", Apply("## Summary", 3, false));
        Equal("ATX indent and separator preservation", "  ###\t Summary", Apply("  ##\t Summary", 3, false));
        Equal("ATX empty title", "# ", Apply("###", 1, false));
        Equal("nonheading hash text preserved", "# #not a heading", Apply("#not a heading", 1, false));
        Equal("single blank current line receives marker", "# ", Apply("", 1, true));
        Equal("single blank selected text remains blank", "", Apply("", 1, false));
        Equal("selected interior blank remains blank", "# first\n\n# second\n", Apply("first\n\nsecond\n", 1, false));
        Equal("CRLF and final delimiter preserved", "## first\r\n\r\n## second\r\n", Apply("first\r\n\r\nsecond\r\n", 2, false));
        Equal("standalone CR delimiter preserved", "# one\r# two", Apply("one\rtwo", 1, false));
        Equal("setext h1 converts to requested level", "### Heading", Apply("Heading\n====", 3, false));
        Equal("setext h2 converts to requested level", "# Heading", Apply("Heading\r\n----", 1, false));
        Equal("setext body and delimiter preserved", "## Heading\r\n## Next", Apply("Heading\r\n====\r\nNext", 2, false));
        Equal("orphan setext underline treated as text", "\r\n# ====", Apply("\r\n====", 1, false));
        Equal("mixed delimiters preserved", "# one\r\n# two\n# three\r# four", Apply("one\r\ntwo\nthree\rfour", 1, false));
        Equal("invalid level leaves text unchanged", "## Keep", Apply("## Keep", 4, false));
        Equal("setext underline classifier", "true", IsSetext("  ---  ").ToString().ToLowerInvariant());
        Check(!IsSetext("- - -"), "spaced thematic break is not setext underline");
        Check(!IsSetext("x---"), "text suffix is not setext underline");

        Console.WriteLine("PASS=" + passed + "; FAIL=0");
        return 0;
    }
}
