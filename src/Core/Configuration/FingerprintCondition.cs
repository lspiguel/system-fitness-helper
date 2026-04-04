// <copyright file="FingerprintCondition.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.RegularExpressions;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// A single predicate that evaluates a named field of a <see cref="SystemFitnessHelper.Fingerprinting.ProcessFingerprint"/>
/// against a value using a comparison operator (eq, neq, regex, gt, lt).
/// </summary>
public sealed class FingerprintCondition
{
    /// <summary>
    /// Gets the value of the field represented by this property.
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// Gets the operator used for comparison in a query expression.
    /// </summary>
    /// <remarks>The default value is "eq", representing the equality operator. This property is typically
    /// used to specify the type of comparison (such as equality, greater than, or less than) when constructing dynamic
    /// queries.</remarks>
    public string Op { get; init; } = "eq";

    /// <summary>
    /// Gets the string value represented by this property.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Evaluates the specified process fingerprint against the configured operation and value.
    /// </summary>
    /// <remarks>Supported operations include equality, inequality, regular expression matching, and numeric
    /// comparisons. The evaluation is case-insensitive for string operations. If the operation is not recognized, the
    /// method returns false.</remarks>
    /// <param name="fp">The process fingerprint to evaluate. Must not be null.</param>
    /// <returns>true if the fingerprint satisfies the operation and value criteria; otherwise, false.</returns>
    public bool Evaluate(ProcessFingerprint fp)
    {
        var fieldValue = this.GetFieldValue(fp);
        return this.Op.ToLowerInvariant() switch
        {
            "eq" => string.Equals(fieldValue, this.Value, StringComparison.OrdinalIgnoreCase),
            "neq" => !string.Equals(fieldValue, this.Value, StringComparison.OrdinalIgnoreCase),
            "regex" => fieldValue != null && Regex.IsMatch(fieldValue, this.Value, RegexOptions.IgnoreCase),
            "gt" => CompareNumeric(fieldValue, this.Value) > 0,
            "lt" => CompareNumeric(fieldValue, this.Value) < 0,
            _ => false,
        };
    }

    private static int CompareNumeric(string? fieldValue, string conditionValue)
    {
        if (!long.TryParse(fieldValue, out var fv) || !long.TryParse(conditionValue, out var cv))
        {
            return 0;
        }

        return fv.CompareTo(cv);
    }

    private string? GetFieldValue(ProcessFingerprint fp) =>
        this.Field.ToLowerInvariant() switch
        {
            "processname" => fp.ProcessName,
            "executablepath" => fp.ExecutablePath,
            "commandline" => fp.CommandLine,
            "publisher" => fp.Publisher,
            "parentprocessname" => fp.ParentProcessName,
            "servicename" => fp.ServiceName,
            "servicedisplayname" => fp.ServiceDisplayName,
            "workingsetbytes" => fp.WorkingSetBytes.ToString(),
            "servicestatus" => fp.ServiceStatus?.ToString(),
            "isservice" => fp.IsService.ToString(),
            _ => null,
        };
}
