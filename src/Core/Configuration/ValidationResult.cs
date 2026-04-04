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
    private readonly List<string> errors = [];
    private readonly List<string> warnings = [];

    /// <summary>
    /// Gets the collection of error messages associated with the current operation or object.
    /// </summary>
    public IReadOnlyList<string> Errors => this.errors;

    /// <summary>
    /// Gets the collection of warning messages associated with the current operation or object.
    /// </summary>
    public IReadOnlyList<string> Warnings => this.warnings;

    /// <summary>
    /// Gets a value indicating whether the current state is valid.
    /// </summary>
    public bool IsValid => this.errors.Count == 0;

    /// <summary>
    /// Adds an error message to the collection of errors.
    /// </summary>
    /// <param name="message">The error message to add. Cannot be null.</param>
    internal void AddError(string message) => this.errors.Add(message);

    /// <summary>
    /// Adds a warning message to the collection of warnings.
    /// </summary>
    /// <param name="message">The warning message to add. Cannot be null.</param>
    internal void AddWarning(string message) => this.warnings.Add(message);
}
