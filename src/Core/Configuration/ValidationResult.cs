// <copyright file="ValidationResult.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Accumulates errors and warnings produced during rule-set validation.
/// <see cref="IsValid"/> is <c>true</c> only when no errors have been added.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool IsValid => _errors.Count == 0;

    internal void AddError(string message) => _errors.Add(message);
    internal void AddWarning(string message) => _warnings.Add(message);
}
