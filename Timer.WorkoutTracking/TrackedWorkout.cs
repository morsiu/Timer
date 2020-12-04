﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Timer.WorkoutPlans;

namespace Timer.WorkoutTracking
{
    internal abstract class TrackedWorkout : ITrackedWorkout
    {
        public abstract T Match<T>(
            Func<Round, WorkoutPlans.Index, Duration, T> @break,
            Func<Round, WorkoutPlans.Index, Duration, T> exercise,
            Func<Duration, T> warmup);

        public abstract Task Track(CancellationToken cancellationToken);
    }
}
