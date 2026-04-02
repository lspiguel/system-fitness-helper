// <copyright file="ActionPlan.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Actions;

/// <summary>
/// Immutable value object (record) that pairs a matched <see cref="ProcessFingerprint"/> with the
/// <see cref="ActionType"/> to execute and the ID of the rule that triggered it.
/// </summary>
public sealed record ActionPlan(ProcessFingerprint Fingerprint, ActionType Action, string RuleId);
