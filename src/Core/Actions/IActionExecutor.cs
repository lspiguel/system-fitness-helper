// <copyright file="IActionExecutor.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Actions;

/// <summary>
/// Executes a planned remediation action against a process or service.
/// Implementations are platform-specific (e.g. <see cref="WindowsActionExecutor"/>).
/// </summary>
public interface IActionExecutor
{
    ActionResult Execute(ActionPlan plan);
}
