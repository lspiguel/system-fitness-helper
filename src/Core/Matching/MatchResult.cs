// <copyright file="MatchResult.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

/// <summary>
/// Immutable value object (record) produced by <see cref="IRuleMatcher"/> that pairs a
/// <see cref="ProcessFingerprint"/> with the <see cref="Rule"/> it satisfied.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Used as a record.")]
public sealed record MatchResult(ProcessFingerprint Fingerprint, Rule Rule);
