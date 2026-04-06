// <copyright file="ConfigResult.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Services;

/// <summary>
/// The result returned by <see cref="IConfigService.GetConfig"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Used as a record.")]
public sealed record ConfigResult(
    RuleSetsConfig? Config,
    IReadOnlyList<string> AvailableRuleSetNames,
    ValidationResult Validation,
    string? ErrorMessage,
    int ExitCode);
