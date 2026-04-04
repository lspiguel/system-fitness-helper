// <copyright file="Rule.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Defines a single matching rule: a set of <see cref="FingerprintCondition"/> predicates combined
/// by <see cref="ConditionLogic"/> ("And" or "Or"), and the <see cref="ActionType"/> to apply when matched.
/// </summary>
public sealed class Rule
{
    /// <summary>
    /// Gets the unique identifier for the instance.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional description associated with this instance.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether the feature or component is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the collection of conditions that define the fingerprint criteria.
    /// </summary>
    public List<FingerprintCondition> Conditions { get; init; } = [];

    /// <summary>
    /// Gets the logical operator used to combine multiple conditions.
    /// </summary>
    /// <remarks>The value determines how individual conditions are evaluated together. Common values include
    /// "And" to require all conditions to be met, or "Or" to require at least one condition to be met.</remarks>
    public string ConditionLogic { get; init; } = "And";

    /// <summary>
    /// Gets the action to be performed by the current instance.
    /// </summary>
    public ActionType Action { get; init; } = ActionType.None;
}
