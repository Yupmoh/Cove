using Cove.Engine.Knowledge;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CommentAnchorEngineTests
{
    private static readonly CommentAnchorEngine Engine = new();

    private static IReadOnlyList<string> Lines(params string[] lines) => lines.ToList();

    private static string Hash(string line) => CommentAnchorEngine.ComputeContextHash(line);

    [Fact]
    public void ReAnchor_NoHunks_KeepsLine()
    {
        var anchor = new CommentAnchor("file.cs", 2, null);
        var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = Lines("a", "b", "c") };

        var result = Engine.ReAnchor(anchor, [], files);

        Assert.NotNull(result);
        Assert.Equal("file.cs", result!.FilePath);
        Assert.Equal(2, result.NewLine);
        Assert.False(result.Orphaned);
    }

    [Fact]
    public void ReAnchor_InsertAbove_ShiftsDown()
    {
        var anchor = new CommentAnchor("file.cs", 3, null);
        var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = Lines("l1", "l2", "l3", "l4", "l5") };
        var hunks = new List<DiffHunk>
        {
            new("file.cs", "file.cs", 1, 0, 1, 2),
        };

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.Equal(5, result!.NewLine);
        Assert.False(result.Orphaned);
    }

    [Fact]
    public void ReAnchor_InsertBelow_KeepsLine()
    {
        var anchor = new CommentAnchor("file.cs", 3, null);
        var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = Lines("l1", "l2", "l3", "l4", "l5") };
        var hunks = new List<DiffHunk>
        {
            new("file.cs", "file.cs", 6, 0, 6, 3),
        };

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.Equal(3, result!.NewLine);
        Assert.False(result.Orphaned);
    }

    [Fact]
    public void ReAnchor_DeleteLineAbove_ShiftsUp()
    {
        var anchor = new CommentAnchor("file.cs", 4, null);
        var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = Lines("l1", "l2", "l3") };
        var hunks = new List<DiffHunk>
        {
            new("file.cs", "file.cs", 2, 1, 2, 0),
        };

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.Equal(3, result!.NewLine);
        Assert.False(result.Orphaned);
    }

    [Fact]
    public void ReAnchor_DeleteCommentedLine_Orphans()
    {
        var anchor = new CommentAnchor("file.cs", 3, null);
        var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = Lines("l1", "l2") };
        var hunks = new List<DiffHunk>
        {
            new("file.cs", "file.cs", 3, 1, 3, 0),
        };

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.True(result!.Orphaned);
    }

    [Fact]
    public void ReAnchor_Rename_FollowsToNewPath()
    {
        var anchor = new CommentAnchor("old.cs", 3, null);
        var files = new Dictionary<string, IReadOnlyList<string>> { ["new.cs"] = Lines("l1", "l2", "l3", "l4", "l5") };
        var hunks = new List<DiffHunk>
        {
            new("old.cs", "new.cs", 1, 5, 1, 5),
        };

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.Equal("new.cs", result!.FilePath);
        Assert.Equal(3, result.NewLine);
        Assert.False(result.Orphaned);
    }

    [Fact]
    public void ReAnchor_ContextHashMatch_KeepsAnchored()
    {
        var contextLine = "var x = ComputeHash(input);";
        var anchor = new CommentAnchor("file.cs", 3, Hash(contextLine));
        var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = Lines("line1", "line2", contextLine, "line4") };
        var hunks = new List<DiffHunk>
        {
            new("file.cs", "file.cs", 1, 1, 1, 2),
        };

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.Equal(4, result!.NewLine);
        Assert.False(result.Orphaned);
    }

    [Fact]
    public void ReAnchor_ContextHashMismatch_Orphans()
    {
        var originalLine = "var x = ComputeHash(input);";
        var changedLine = "var y = DifferentCall();";
        var anchor = new CommentAnchor("file.cs", 3, Hash(originalLine));
        var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = Lines("line1", "line2", changedLine, "line4") };
        var hunks = new List<DiffHunk>
        {
            new("file.cs", "file.cs", 3, 1, 3, 1),
        };

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.True(result!.Orphaned);
    }

    [Fact]
    public void ReAnchor_FileDeleted_Orphans()
    {
        var anchor = new CommentAnchor("file.cs", 5, null);
        var files = new Dictionary<string, IReadOnlyList<string>>();
        var hunks = new List<DiffHunk>();

        var result = Engine.ReAnchor(anchor, hunks, files);

        Assert.True(result!.Orphaned);
    }

    [Fact]
    public void Fuzz_NeverWrongAnchored_InsertsPreserveRelativePosition()
    {
        var rng = new Random(42);
        for (int iter = 0; iter < 200; iter++)
        {
            var originalLines = Enumerable.Range(0, 20).Select(_ => $"line{rng.Next(10000)}").ToList();
            var commentLine = rng.Next(1, 21);
            var insertAt = rng.Next(1, 21);

            var newLines = new List<string>(originalLines);
            newLines.Insert(insertAt - 1, "INSERTED");

            var hunks = new List<DiffHunk>
            {
                new("file.cs", "file.cs", insertAt, 0, insertAt, 1),
            };
            var anchor = new CommentAnchor("file.cs", commentLine, null);
            var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = newLines };

            var result = Engine.ReAnchor(anchor, hunks, files);

            Assert.NotNull(result);
            Assert.False(result!.Orphaned);
            Assert.Equal(originalLines[commentLine - 1], newLines[result.NewLine - 1]);
        }
    }

    [Fact]
    public void Fuzz_NeverWrongAnchored_DeletesPreserveRelativePosition()
    {
        var rng = new Random(99);
        for (int iter = 0; iter < 200; iter++)
        {
            var originalLines = Enumerable.Range(0, 20).Select(_ => $"line{rng.Next(10000)}").ToList();
            var commentLine = rng.Next(1, 21);
            var deleteStart = rng.Next(1, 21);
            var deleteCount = rng.Next(1, 4);

            if (commentLine >= deleteStart && commentLine < deleteStart + deleteCount)
                continue;

            var newLines = new List<string>(originalLines);
            var actualDelete = Math.Min(deleteCount, newLines.Count - deleteStart + 1);
            if (actualDelete <= 0) continue;
            newLines.RemoveRange(deleteStart - 1, actualDelete);

            var hunks = new List<DiffHunk>
            {
                new("file.cs", "file.cs", deleteStart, deleteCount, deleteStart, 0),
            };
            var anchor = new CommentAnchor("file.cs", commentLine, null);
            var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = newLines };

            var result = Engine.ReAnchor(anchor, hunks, files);

            Assert.NotNull(result);
            Assert.False(result!.Orphaned);
            Assert.Equal(originalLines[commentLine - 1], newLines[result.NewLine - 1]);
        }
    }

    [Fact]
    public void Fuzz_RenameAlwaysFollows()
    {
        var rng = new Random(7);
        for (int iter = 0; iter < 100; iter++)
        {
            var lineCount = rng.Next(5, 30);
            var commentLine = rng.Next(1, lineCount + 1);
            var originalLines = Enumerable.Range(0, lineCount).Select(_ => $"line{rng.Next(10000)}").ToList();

            var anchor = new CommentAnchor("old.cs", commentLine, null);
            var files = new Dictionary<string, IReadOnlyList<string>> { ["new.cs"] = originalLines };
            var hunks = new List<DiffHunk>
            {
                new("old.cs", "new.cs", 1, lineCount, 1, lineCount),
            };

            var result = Engine.ReAnchor(anchor, hunks, files);

            Assert.Equal("new.cs", result!.FilePath);
            Assert.Equal(commentLine, result.NewLine);
            Assert.False(result.Orphaned);
        }
    }

    [Fact]
    public void Fuzz_MultiHunk_MixedInsertsAndDeletes_NeverWrongAnchored()
    {
        var rng = new Random(314);
        for (int iter = 0; iter < 300; iter++)
        {
            var originalLines = Enumerable.Range(0, 30).Select(_ => $"line{rng.Next(100000)}").ToList();
            var commentLine = rng.Next(1, 31);

            var newLines = new List<string>(originalLines);
            var hunks = new List<DiffHunk>();
            var newCursor = 1;
            var oldCursor = 1;
            while (oldCursor <= originalLines.Count)
            {
                var op = rng.Next(3);
                var chunkSize = rng.Next(1, 4);

                if (op == 0)
                {
                    var insertedContent = Enumerable.Range(0, chunkSize).Select(_ => $"ins{rng.Next(100000)}").ToList();
                    newLines.InsertRange(newCursor - 1, insertedContent);
                    hunks.Add(new DiffHunk("file.cs", "file.cs", oldCursor, 0, newCursor, chunkSize));
                    newCursor += chunkSize;
                }
                else if (op == 1)
                {
                    var wouldDeleteComment = commentLine >= oldCursor && commentLine < oldCursor + chunkSize;
                    if (wouldDeleteComment)
                    {
                        newLines.InsertRange(newCursor - 1, Enumerable.Range(0, 1).Select(_ => $"ins{rng.Next(100000)}"));
                        hunks.Add(new DiffHunk("file.cs", "file.cs", oldCursor, 0, newCursor, 1));
                        newCursor += 1;
                    }
                    else
                    {
                        var actualDelete = Math.Min(chunkSize, originalLines.Count - oldCursor + 1);
                        if (actualDelete <= 0) { oldCursor += 1; continue; }
                        newLines.RemoveRange(newCursor - 1, actualDelete);
                        hunks.Add(new DiffHunk("file.cs", "file.cs", oldCursor, actualDelete, newCursor, 0));
                        oldCursor += actualDelete;
                    }
                }
                else
                {
                    var keep = Math.Min(chunkSize, originalLines.Count - oldCursor + 1);
                    oldCursor += keep;
                    newCursor += keep;
                }
            }

            var anchor = new CommentAnchor("file.cs", commentLine, null);
            var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = newLines };

            var result = Engine.ReAnchor(anchor, hunks, files);

            Assert.NotNull(result);
            Assert.False(result!.Orphaned);
            Assert.Equal(originalLines[commentLine - 1], newLines[result.NewLine - 1]);
        }
    }

    [Fact]
    public void Fuzz_Move_PreservesContent_OrphansDeletedLine()
    {
        var rng = new Random(271);
        for (int iter = 0; iter < 200; iter++)
        {
            var originalLines = Enumerable.Range(0, 20).Select(_ => $"line{rng.Next(100000)}").ToList();
            var commentLine = rng.Next(1, 21);
            var moveFrom = rng.Next(1, 21);
            var moveTo = rng.Next(1, 21);

            if (moveFrom == moveTo)
                continue;

            var movedContent = originalLines[moveFrom - 1];
            var newLines = new List<string>(originalLines);
            newLines.RemoveAt(moveFrom - 1);
            var insertAt = moveTo > moveFrom ? moveTo - 1 : moveTo - 1;
            if (insertAt > newLines.Count) insertAt = newLines.Count;
            newLines.Insert(insertAt, movedContent);

            var deleteHunk = new DiffHunk("file.cs", "file.cs", moveFrom, 1, moveFrom, 0);
            var insertOldStart = moveTo > moveFrom ? moveTo + 1 : moveTo;
            var insertHunk = new DiffHunk("file.cs", "file.cs", insertOldStart, 0, moveTo, 1);
            var hunks = new List<DiffHunk> { deleteHunk, insertHunk };

            var anchor = new CommentAnchor("file.cs", commentLine, null);
            var files = new Dictionary<string, IReadOnlyList<string>> { ["file.cs"] = newLines };

            var result = Engine.ReAnchor(anchor, hunks, files);

            Assert.NotNull(result);
            if (commentLine == moveFrom)
            {
                Assert.True(result!.Orphaned, $"iter {iter}: moved line should orphan (comment on moved line)");
            }
            else
            {
                Assert.False(result!.Orphaned, $"iter {iter}: line {commentLine} orphaned unexpectedly (moveFrom={moveFrom}, moveTo={moveTo})");
                Assert.Equal(originalLines[commentLine - 1], newLines[result.NewLine - 1]);
            }
        }
    }

    [Fact]
    public void Fuzz_Move_WithRename_FollowsContent()
    {
        var rng = new Random(618);
        for (int iter = 0; iter < 100; iter++)
        {
            var lineCount = rng.Next(10, 30);
            var originalLines = Enumerable.Range(0, lineCount).Select(_ => $"line{rng.Next(100000)}").ToList();
            var commentLine = rng.Next(1, lineCount + 1);

            var newLines = new List<string>(originalLines);
            var anchor = new CommentAnchor("old.cs", commentLine, null);
            var files = new Dictionary<string, IReadOnlyList<string>> { ["new.cs"] = newLines };
            var hunks = new List<DiffHunk>
            {
                new("old.cs", "new.cs", 1, lineCount, 1, lineCount),
            };

            var result = Engine.ReAnchor(anchor, hunks, files);

            Assert.Equal("new.cs", result!.FilePath);
            Assert.Equal(commentLine, result.NewLine);
            Assert.False(result.Orphaned);
            Assert.Equal(originalLines[commentLine - 1], newLines[result.NewLine - 1]);
        }
    }
}
