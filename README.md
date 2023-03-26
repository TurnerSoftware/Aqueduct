<div align="center">


# Aqueduct
Utilities and extension methods for working with streams and pipes

![Build](https://img.shields.io/github/actions/workflow/status/TurnerSoftware/aqueduct/build.yml?branch=main)
[![Codecov](https://img.shields.io/codecov/c/github/turnersoftware/aqueduct/main.svg)](https://codecov.io/gh/TurnerSoftware/Aqueduct)
[![NuGet](https://img.shields.io/nuget/v/TurnerSoftware.Aqueduct.svg)](https://www.nuget.org/packages/TurnerSoftware.Aqueduct/)
</div>

## Overview
Aqueduct provides some useful, albeit niche, utilities for working with streams and pipes.

### Pipe/Stream Bifurcation

Allows you to read from a single pipe or stream into multiple targets.
This is useful for cases where you can't buffer the original stream into memory or you're working with a source you can't seek.
Internally it uses individual pipes per target to allow independent processing and minimal memory overhead.

Each bifurcation target has individual options for:
- The reader which processes the data
- An exception handler triggered on failure of _any_ target
- Control of the number of bytes for blocking/resuming writes to the target
- The maximum number of bytes to write to the specific target

Additionally, the bifurcation process overall has options for:
- The minimum read buffer size of the source pipe or stream
- Whether to leave the stream open after bifurcation
- Allow exceptions from readers to bubble out to the calling code
- Cancellation token for reading/writing process

Example usage of pipe/stream bifurcation:

```csharp
await myStream.BifurcatedReadAsync(
	new BifurcationTargetConfig(
		async (Stream stream, CancellationToken cancellationToken) =>
		{
			using var fileStream = File.OpenWrite("some-file-path.bin");
			await stream.CopyToAsync(fileStream);
		},
		maxTotalBytes: 1024
	),
	new BifurcationTargetConfig(
		async (PipeReader reader, CancellationToken cancellationToken) =>
		{
			await someService.ProcessData(reader);
		}
	)
);
```

## Licensing and Support

Aqueduct is licensed under the MIT license. It is free to use in personal and commercial projects.

There are [support plans](https://turnersoftware.com.au/support-plans) available that cover all active [Turner Software OSS projects](https://github.com/TurnerSoftware).
Support plans provide private email support, expert usage advice for our projects, priority bug fixes and more.
These support plans help fund our OSS commitments to provide better software for everyone.
