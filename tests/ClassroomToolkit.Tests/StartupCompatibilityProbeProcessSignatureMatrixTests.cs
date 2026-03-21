using System.Text.Json;
using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityProbeProcessSignatureMatrixTests
{
    private static readonly SignatureMatrix Matrix = LoadMatrix();

    [Theory]
    [MemberData(nameof(MatrixCases))]
    public void TryDescribePresentationProcessMatch_MatrixShouldMatchExpected(
        string processName,
        bool expected,
        string evidenceContains)
    {
        var matched = StartupCompatibilityProbe.TryDescribePresentationProcessMatch(
            processName,
            classifierOverridesJson: null,
            out var evidence);

        matched.Should().Be(expected);
        if (expected)
        {
            evidence.Should().Contain(evidenceContains);
        }
        else
        {
            evidence.Should().BeEmpty();
        }
    }

    public static IEnumerable<object[]> MatrixCases()
    {
        return Matrix.Cases.Select(testCase => new object[]
        {
            testCase.ProcessName,
            testCase.Expected,
            testCase.EvidenceContains
        });
    }

    private static SignatureMatrix LoadMatrix()
    {
        var path = TestPathHelper.ResolveRepoPath(
            "tests",
            "ClassroomToolkit.Tests",
            "Fixtures",
            "startup-presentation-process-signature-matrix.json");
        var json = File.ReadAllText(path);
        var matrix = JsonSerializer.Deserialize<SignatureMatrix>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        if (matrix is null)
        {
            throw new InvalidOperationException("Failed to deserialize startup process signature matrix.");
        }

        return matrix with
        {
            Cases = matrix.Cases ?? Array.Empty<SignatureCase>()
        };
    }

    private sealed record SignatureMatrix(
        IReadOnlyList<SignatureCase> Cases);

    private sealed record SignatureCase(
        string ProcessName,
        bool Expected,
        string EvidenceContains);
}
