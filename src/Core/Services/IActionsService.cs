// <copyright file="IActionsService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Services;

/// <summary>
/// Builds dry-run action plans without executing them.
/// </summary>
public interface IActionsService
{
    /// <summary>
    /// Evaluates what actions would be taken for all matched processes.
    /// </summary>
    /// <param name="configPath">Explicit path to rules.json, or <c>null</c> to auto-discover.</param>
    /// <param name="ruleSetName">Name of the ruleset to use, or <c>null</c> to use the default.</param>
    /// <returns>An <see cref="ActionsResult"/> containing all evaluated plans.</returns>
    ActionsResult GetActions(string? configPath, string? ruleSetName);
}
