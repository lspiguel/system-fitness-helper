// <copyright file="IExecuteService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Services;

/// <summary>
/// Executes the action plans produced by the safety-guarded matching pipeline.
/// </summary>
public interface IExecuteService
{
    /// <summary>
    /// Executes all allowed action plans for matched processes.
    /// </summary>
    /// <param name="configPath">Explicit path to rules.json, or <c>null</c> to auto-discover.</param>
    /// <returns>An <see cref="ExecuteResult"/> with per-plan outcomes.</returns>
    ExecuteResult Execute(string? configPath);
}
