using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timer.WorkoutPlans;
using Timer.WorkoutTracking.Sound;
using Timer.WorkoutTracking.Sound.NAudio;
using Timer.WorkoutTracking.Visual;

namespace Timer
{
    internal sealed class TrackWorkoutPlanCommand : DependencyObject, ICommand
    {
        public static readonly DependencyProperty RoundCountProperty =
            DependencyProperty.Register(
                nameof(RoundCount),
                typeof(Count?),
                typeof(TrackWorkoutPlanCommand),
                new PropertyMetadata(null, RoundCountChanged));

        public static readonly DependencyProperty WorkoutPlanProperty =
            DependencyProperty.Register(
                nameof(WorkoutPlan),
                typeof(Func<WorkoutPlan, WorkoutPlan>),
                typeof(TrackWorkoutPlanCommand),
                new PropertyMetadata(null, WorkoutRoundChanged));

        public static readonly DependencyProperty WorkoutsOfCurrentSegmentProperty;
        private static readonly DependencyPropertyKey WorkoutsOfCurrentSegmentPropertyKey;

        private readonly CancelCommand _cancel = new CancelCommand();
        private bool _running;

        static TrackWorkoutPlanCommand()
        {
            WorkoutsOfCurrentSegmentPropertyKey = 
                DependencyProperty.RegisterReadOnly(
                    nameof(WorkoutsOfCurrentSegment),
                    typeof(object),
                    typeof(TrackWorkoutPlanCommand),
                    new PropertyMetadata(null));
            WorkoutsOfCurrentSegmentProperty = WorkoutsOfCurrentSegmentPropertyKey.DependencyProperty;
        }

        public event EventHandler CanExecuteChanged;

        public ICommand Cancel => _cancel;

        public Count? RoundCount
        {
            get => (Count?) GetValue(RoundCountProperty);
            set => SetValue(RoundCountProperty, value);
        }

        public Func<WorkoutPlan, WorkoutPlan> WorkoutPlan
        {
            get => (Func<WorkoutPlan, WorkoutPlan>) GetValue(WorkoutPlanProperty);
            set => SetValue(WorkoutPlanProperty, value);
        }

        public object WorkoutsOfCurrentSegment
        {
            get => GetValue(WorkoutsOfCurrentSegmentProperty);
            private set => SetValue(WorkoutsOfCurrentSegmentPropertyKey, value);
        }

        public async void Execute(object parameter)
        {
            if (_running || !(WorkoutPlan() is WorkoutPlan workoutPlan)) return;
            try
            {
                _running = true;
                var cancellation = new CancellationTokenSource();
                _cancel.Source = cancellation;
                RaiseCanExecuteChanged();
                await Task.WhenAll(RunSoundTracking(), RunVisualTracking());

                async Task RunVisualTracking()
                {
                    try
                    {
                        var visualTracking = new VisualTrackingOfWorkout(workoutPlan, Dispatcher);
                        WorkoutsOfCurrentSegment = visualTracking.WorkoutsOfCurrentRound;
                        await visualTracking.Run(cancellation.Token);

                    }
                    finally
                    {
                        WorkoutsOfCurrentSegment = null;
                    }
                }

                async Task RunSoundTracking()
                {
                    using (var soundFactory = new NAudioSoundFactory())
                    {
                        await new SoundTrackingOfWorkout(workoutPlan, soundFactory)
                            .Run(cancellation.Token);
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                _cancel.Source = null;
                _running = false;
                RaiseCanExecuteChanged();
            }

            WorkoutPlan WorkoutPlan()
            {
                return this.WorkoutPlan != null && RoundCount != null
                    ? this.WorkoutPlan(new WorkoutPlan().WithRound(RoundCount.Value).WithCountdown(WorkoutPlans.Duration.FromSeconds(3)))
                    : default;
            }
        }

        public bool CanExecute(object parameter) =>
            !_running && WorkoutPlan != null && RoundCount != null;

        private static void RoundCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrackWorkoutPlanCommand self)
            {
                self.RaiseCanExecuteChanged();
            }
        }

        private static void WorkoutRoundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrackWorkoutPlanCommand self)
            {
                self.RaiseCanExecuteChanged();
            }
        }

        private void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}