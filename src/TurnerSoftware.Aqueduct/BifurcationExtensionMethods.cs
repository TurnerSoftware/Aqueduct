﻿using System.IO.Pipelines;

namespace TurnerSoftware.Aqueduct;

/// <summary>
/// Extension methods specific for bifurcation.
/// </summary>
public static class BifurcationExtensionMethods
{
	/// <summary>
	/// Performs bifurcation with <paramref name="sourceReader"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceReader">The source to read from.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns>A list of results from the targets, in the order the targets were provided.</returns>
	public static Task<IReadOnlyList<TResult?>> BifurcatedReadAsync<TResult>(this PipeReader sourceReader, params BifurcationTargetConfig<TResult>[] targetConfigs)
		=> BifurcatedReadAsync(sourceReader, BifurcationSourceConfig.DefaultConfig, targetConfigs);
	/// <summary>
	/// Performs bifurcation with <paramref name="sourceReader"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceReader">The source to read from.</param>
	/// <param name="sourceConfig">Source-specific configuration for reading.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns>A list of results from the targets, in the order the targets were provided.</returns>
	public static Task<IReadOnlyList<TResult?>> BifurcatedReadAsync<TResult>(this PipeReader sourceReader, BifurcationSourceConfig sourceConfig, params BifurcationTargetConfig<TResult>[] targetConfigs)
		=> PipeBifurcation.BifurcatedReadAsync(sourceReader, sourceConfig, targetConfigs);

	/// <summary>
	/// Performs bifurcation with <paramref name="sourceStream"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceStream">The source to read from.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns>A list of results from the targets, in the order the targets were provided.</returns>
	public static Task<IReadOnlyList<TResult?>> BifurcatedReadAsync<TResult>(this Stream sourceStream, params BifurcationTargetConfig<TResult>[] targetConfigs)
		=> BifurcatedReadAsync(sourceStream, StreamBifurcationSourceConfig.DefaultStreamConfig, targetConfigs);
	/// <summary>
	/// Performs bifurcation with <paramref name="sourceStream"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceStream">The source to read from.</param>
	/// <param name="sourceConfig">Source-specific configuration for reading.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns>A list of results from the targets, in the order the targets were provided.</returns>
	public static Task<IReadOnlyList<TResult?>> BifurcatedReadAsync<TResult>(this Stream sourceStream, StreamBifurcationSourceConfig sourceConfig, params BifurcationTargetConfig<TResult>[] targetConfigs)
	{
		var sourceReader = PipeReader.Create(sourceStream, new StreamPipeReaderOptions(leaveOpen: sourceConfig.LeaveOpen));
		return PipeBifurcation.BifurcatedReadAsync(sourceReader, sourceConfig, targetConfigs);
	}

	/// <summary>
	/// Performs bifurcation with <paramref name="sourceReader"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceReader">The source to read from.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns></returns>
	public static Task BifurcatedReadAsync(this PipeReader sourceReader, params BifurcationTargetConfig[] targetConfigs)
		=> BifurcatedReadAsync(sourceReader, BifurcationSourceConfig.DefaultConfig, targetConfigs);
	/// <summary>
	/// Performs bifurcation with <paramref name="sourceReader"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceReader">The source to read from.</param>
	/// <param name="sourceConfig">Source-specific configuration for reading.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns></returns>
	public static Task BifurcatedReadAsync(this PipeReader sourceReader, BifurcationSourceConfig sourceConfig, params BifurcationTargetConfig[] targetConfigs)
		=> PipeBifurcation.BifurcatedReadAsync(sourceReader, sourceConfig, targetConfigs);

	/// <summary>
	/// Performs bifurcation with <paramref name="sourceStream"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceStream">The source to read from.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns></returns>
	public static Task BifurcatedReadAsync(this Stream sourceStream, params BifurcationTargetConfig[] targetConfigs)
		=> BifurcatedReadAsync(sourceStream, StreamBifurcationSourceConfig.DefaultStreamConfig, targetConfigs);
	/// <summary>
	/// Performs bifurcation with <paramref name="sourceStream"/>, splitting the resulting data into multiple <paramref name="targetConfigs"/>.
	/// </summary>
	/// <param name="sourceStream">The source to read from.</param>
	/// <param name="sourceConfig">Source-specific configuration for reading.</param>
	/// <param name="targetConfigs">The targets to provide the bifurcated data to.</param>
	/// <returns></returns>
	public static Task BifurcatedReadAsync(this Stream sourceStream, StreamBifurcationSourceConfig sourceConfig, params BifurcationTargetConfig[] targetConfigs)
	{
		var sourceReader = PipeReader.Create(sourceStream, new StreamPipeReaderOptions(leaveOpen: sourceConfig.LeaveOpen));
		return PipeBifurcation.BifurcatedReadAsync(sourceReader, sourceConfig, targetConfigs);
	}
}
