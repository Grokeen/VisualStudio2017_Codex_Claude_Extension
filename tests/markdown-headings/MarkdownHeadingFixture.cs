using System;
using VS2017.AIAssistant.Views;

internal static class MarkdownHeadingFixture
{
    private static int passed;

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

    private static int Main()
    {
        Equal("plain line h1", "# Summary", MarkdownHeadingEditing.Apply("Summary", 1, false));
        Equal("plain line h2", "## Summary", MarkdownHeadingEditing.Apply("Summary", 2, false));
        Equal("ATX level replacement", "### Summary", MarkdownHeadingEditing.Apply("## Summary", 3, false));
        Equal("ATX indent and separator preservation", "  ###\t Summary", MarkdownHeadingEditing.Apply("  ##\t Summary", 3, false));
        Equal("ATX empty title", "# ", MarkdownHeadingEditing.Apply("###", 1, false));
        Equal("nonheading hash text preserved", "# #not a heading", MarkdownHeadingEditing.Apply("#not a heading", 1, false));
        Equal("single blank current line receives marker", "# ", MarkdownHeadingEditing.Apply("", 1, true));
        Equal("single blank selected text remains blank", "", MarkdownHeadingEditing.Apply("", 1, false));
        Equal("selected interior blank remains blank", "# first\n\n# second\n", MarkdownHeadingEditing.Apply("first\n\nsecond\n", 1, false));
        Equal("CRLF and final delimiter preserved", "## first\r\n\r\n## second\r\n", MarkdownHeadingEditing.Apply("first\r\n\r\nsecond\r\n", 2, false));
        Equal("standalone CR delimiter preserved", "# one\r# two", MarkdownHeadingEditing.Apply("one\rtwo", 1, false));
        Equal("setext h1 converts to requested level", "### Heading", MarkdownHeadingEditing.Apply("Heading\n====", 3, false));
        Equal("setext h2 converts to requested level", "# Heading", MarkdownHeadingEditing.Apply("Heading\r\n----", 1, false));
        Equal("setext body and delimiter preserved", "## Heading\r\n## Next", MarkdownHeadingEditing.Apply("Heading\r\n====\r\nNext", 2, false));
        Equal("orphan setext underline treated as text", "\r\n# ====", MarkdownHeadingEditing.Apply("\r\n====", 1, false));
        Equal("mixed delimiters preserved", "# one\r\n# two\n# three\r# four", MarkdownHeadingEditing.Apply("one\r\ntwo\nthree\rfour", 1, false));
        Equal("invalid level leaves text unchanged", "## Keep", MarkdownHeadingEditing.Apply("## Keep", 4, false));
        Equal("setext underline classifier", "true", MarkdownHeadingEditing.IsSetextUnderline("  ---  ").ToString().ToLowerInvariant());
        Check(!MarkdownHeadingEditing.IsSetextUnderline("- - -"), "spaced thematic break is not setext underline");
        Check(!MarkdownHeadingEditing.IsSetextUnderline("x---"), "text suffix is not setext underline");

        Console.WriteLine("PASS=" + passed + "; FAIL=0");
        return 0;
    }
}
