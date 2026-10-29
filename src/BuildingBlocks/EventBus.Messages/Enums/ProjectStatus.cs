namespace EventBus.Messages.Enums
{
    /// <summary>
    /// Represents the lifecycle status of a landscaping project.
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>Initial lead received, no project yet.</summary>
        Lead = 0,

        /// <summary>Negotiations in progress.</summary>
        Negotiating = 1,

        /// <summary>Proposal sent to client.</summary>
        Proposed = 2,

        /// <summary>Contract signed, project approved.</summary>
        Contracted = 3,

        /// <summary>Design phase — planning the landscape.</summary>
        Design = 4,

        /// <summary>Active execution — work in progress.</summary>
        InProgress = 5,

        /// <summary>Work completed, pending client acceptance.</summary>
        Completed = 6,

        /// <summary>Project cancelled or aborted.</summary>
        Cancelled = 7,

        /// <summary>On hold pending materials, weather, or client decision.</summary>
        OnHold = 8
    }
}
