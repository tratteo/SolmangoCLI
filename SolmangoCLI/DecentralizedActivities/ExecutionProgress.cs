// Copyright Siamango

namespace SolmangoCLI.DecentralizedActivities;

public struct ExecutionProgress
{
    public string Activity { get; init; }

    public int StepCount { get; init; }

    public Step CurrentStep { get; init; }

    public ExecutionProgress(string activity, int stepCount, Step step)
    {
        Activity = activity;
        StepCount = stepCount;
        CurrentStep = step;
    }

    public static ExecutionProgress Idle() => new ExecutionProgress("idle", 1, new Step(1, "Idling", 1F));

    public struct Step
    {
        public int Number { get; init; }

        public string Description { get; init; }

        public float Progress { get; init; }

        public Step(int number, string description, float progress)
        {
            Number = number;
            Description = description;
            Progress = progress;
        }
    }
}