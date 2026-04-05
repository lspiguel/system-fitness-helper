// <copyright file="ActionResultView.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Services;

/// <summary>
/// Flat, JSON-serializable view model representing the outcome of one executed action plan.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Used as a record.")]
public sealed record ActionResultView(
    string ProcessName,
    int ProcessId,
    string? ServiceName,
    string RuleId,
    ActionType Action,
    bool Success,
    string Message);
