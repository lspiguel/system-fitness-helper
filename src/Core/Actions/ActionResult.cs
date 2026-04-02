// <copyright file="ActionResult.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Actions;

/// <summary>
/// Represents the outcome of executing an <see cref="ActionPlan"/>.
/// Use the static factory methods <see cref="Ok"/> and <see cref="Fail"/> to construct instances.
/// </summary>
public sealed class ActionResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public Exception? Exception { get; init; }

    public static ActionResult Ok(string message)
        => new () { Success = true, Message = message };

    public static ActionResult Fail(string message, Exception? ex = null)
        => new () { Success = false, Message = message, Exception = ex };
}
