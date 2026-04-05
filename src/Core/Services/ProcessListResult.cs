// <copyright file="ProcessListResult.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;

namespace SystemFitnessHelper.Services;

/// <summary>
/// The result returned by <see cref="IListService.GetProcessList"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Used as a record.")]
public sealed record ProcessListResult(
    IReadOnlyList<ProcessFingerprint> Fingerprints,
    IReadOnlyList<MatchResult> Matches,
    string? ErrorMessage,
    int ExitCode);
