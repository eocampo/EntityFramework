// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Data.Entity.Infrastructure
{
    /// <summary>
    ///     Values that are used as the eventId when logging messages from this assembly via <see cref="ILogger" />.
    /// </summary>
    public enum CoreLoggingEventId
    {
        /// <summary>
        ///     An error occurred while accessing the database.
        /// </summary>
        DatabaseError = 1,

        /// <summary>
        ///     A LINQ query is being compiled.
        /// </summary>
        CompilingQueryModel,

        /// <summary>
        ///     An object model representing a LINQ query was optimized.
        /// </summary>
        OptimizedQueryModel,

        /// <summary>
        ///     A navigation property that was included in a LINQ query is being processed.
        /// </summary>
        IncludingNavigation,

        /// <summary>
        ///     An execution expression was calculated by compiling a LINQ query.
        /// </summary>
        QueryPlan
    }
}
