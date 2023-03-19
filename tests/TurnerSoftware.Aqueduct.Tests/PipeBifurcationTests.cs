using System.IO.Pipelines;
using System.Text;

namespace TurnerSoftware.Aqueduct.Tests;

[TestClass]
public class PipeBifurcationTests
{
    private static PipeReader CreateSource(string value) => PipeReader.Create(new(Encoding.ASCII.GetBytes(value)));
    private static PipeReader CreateSource(byte[] bytes) => PipeReader.Create(new(bytes));

    private static Func<PipeReader, CancellationToken, Task> CreateStringTarget(Func<Task<string>, Task> assertion)
    {
        return (reader, cancellationToken) =>
        {
            static async Task<string> ReadFullAsync(PipeReader reader, CancellationToken cancellationToken)
            {
                using var stream = reader.AsStream();
                using var a = new StreamReader(stream);
                return await a.ReadToEndAsync();
            }
            return assertion(ReadFullAsync(reader, cancellationToken));
        };
    }

    [TestMethod]
    public async Task NoTargets_ThrowsException()
    {
        var source = CreateSource("Test Value");

        await FluentActions.Awaiting(() => PipeBifurcation.BifurcatedReadAsync(source, BifurcationSourceConfig.DefaultConfig))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("No target configurations*");
    }

    [TestMethod]
    public async Task SingleTarget_TargetExceptionBubbles()
    {
        var source = CreateSource("Test Value");

        await FluentActions.Awaiting(() => PipeBifurcation.BifurcatedReadAsync(source, BifurcationSourceConfig.DefaultConfig, new BifurcationTargetConfig(
                CreateStringTarget(async resultTask =>
                {
                    await resultTask;
                    throw new ApplicationException("My processing exception");
                })
            )))
            .Should()
            .ThrowAsync<BifurcationException>()
                .WithInnerException<BifurcationException, ApplicationException>()
                .WithMessage("My processing exception");
    }

    [TestMethod]
    public async Task SingleTarget_DefaultConfig_Success()
    {
        var source = CreateSource("Test Value");

        await PipeBifurcation.BifurcatedReadAsync(source, BifurcationSourceConfig.DefaultConfig, new BifurcationTargetConfig(
            CreateStringTarget(async resultTask =>
            {
                var result = await resultTask;
                result.Should().Be("Test Value");
            })
        ));
    }
}