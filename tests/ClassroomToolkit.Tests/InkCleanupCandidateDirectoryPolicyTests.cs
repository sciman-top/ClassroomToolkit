using System;
using System.Collections.Generic;
using ClassroomToolkit.App.Ink;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkCleanupCandidateDirectoryPolicyTests
{
    [Fact]
    public void Resolve_ShouldIncludeOnlyExistingAndDistinctDirectories()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\base",
            @"C:\ink",
            @"C:\recent"
        };

        var candidates = InkCleanupCandidateDirectoryPolicy.Resolve(
            baseDirectory: @"C:\base",
            inkPhotoRootPath: @"C:\ink",
            recentFolders: new[] { @"C:\recent", @"C:\ink", @"C:\missing" },
            favoriteFolders: new[] { @"C:\recent", @"C:\missing2" },
            directoryExists: existing.Contains);

        candidates.Should().BeEquivalentTo(new[] { @"C:\base", @"C:\ink", @"C:\recent" });
    }

    [Fact]
    public void Resolve_ShouldIgnoreNullOrWhitespacePaths()
    {
        var candidates = InkCleanupCandidateDirectoryPolicy.Resolve(
            baseDirectory: " ",
            inkPhotoRootPath: null,
            recentFolders: new[] { "", "   " },
            favoriteFolders: null,
            directoryExists: _ => true);

        candidates.Should().BeEmpty();
    }
}
